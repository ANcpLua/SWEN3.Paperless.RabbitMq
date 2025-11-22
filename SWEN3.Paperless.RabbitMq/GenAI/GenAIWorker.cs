using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SWEN3.Paperless.RabbitMq.Consuming;
using SWEN3.Paperless.RabbitMq.Models;
using SWEN3.Paperless.RabbitMq.Publishing;
using SWEN3.Paperless.RabbitMq.Schema;

namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Background service that processes GenAI summarization commands from the message queue.
///     <para>
///         Consumes <see cref="GenAICommand" /> messages, generates document summaries using
///         <see cref="ITextSummarizer" />,
///         and publishes <see cref="GenAIEvent" /> results back to the queue for downstream processing.
///     </para>
/// </summary>
/// <remarks>
///     Error handling:
///     <list type="bullet">
///         <item>Transient failures (HTTP errors) trigger message requeue for retry</item>
///         <item>Fatal errors result in failure events being published and messages discarded</item>
///         <item>Empty or invalid text content is acknowledged without processing</item>
///         <item>Graceful shutdown on cancellation token requests</item>
///     </list>
/// </remarks>
public sealed partial class GenAIWorker : BackgroundService
{
    private readonly IRabbitMqConsumerFactory _consumerFactory;
    private readonly ILogger<GenAIWorker> _logger;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ITextSummarizer _summarizer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenAIWorker" /> class.
    /// </summary>
    /// <param name="consumerFactory">Factory for creating RabbitMQ consumers to receive GenAI commands.</param>
    /// <param name="publisher">Publisher for sending GenAI events back to the message queue.</param>
    /// <param name="summarizer">Text summarization service for processing document content.</param>
    /// <param name="logger">Logger instance for diagnostic output and error tracking.</param>
    public GenAIWorker(IRabbitMqConsumerFactory consumerFactory, IRabbitMqPublisher publisher,
        ITextSummarizer summarizer, ILogger<GenAIWorker> logger)
    {
        _consumerFactory = consumerFactory;
        _publisher = publisher;
        _summarizer = summarizer;
        _logger = logger;
    }

    /// <summary>
    ///     Main execution loop that processes GenAI commands from the queue until cancellation is requested.
    /// </summary>
    /// <param name="stoppingToken">Token that signals when the service should stop processing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GenAIWorker started");

        await using var consumer = await _consumerFactory.CreateConsumerAsync<GenAICommand>().ConfigureAwait(false);

        await foreach (var command in consumer.ConsumeAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            await HandleCommandAsync(command, consumer, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("GenAIWorker stopped");
    }

    /// <summary>
    ///     Asynchronously processes a single GenAI command by generating a document summary and publishing the result.
    /// </summary>
    /// <param name="command">
    ///     The <see cref="GenAICommand" /> containing the document ID and the OCR-extracted text to be summarized.
    ///     <para>If the text is empty or whitespace, the command is acknowledged immediately without processing.</para>
    /// </param>
    /// <param name="consumer">
    ///     The <see cref="IRabbitMqConsumer{T}" /> instance used to acknowledge (Ack) or negatively acknowledge (Nack) the
    ///     message
    ///     based on processing success or failure.
    /// </param>
    /// <param name="ct">
    ///     A <see cref="CancellationToken" /> to observe while waiting for asynchronous operations (summarization,
    ///     publishing).
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> representing the asynchronous processing operation.
    /// </returns>
    /// <remarks>
    ///     <para>The method implements specific error handling strategies:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <term>Success</term>
    ///             <description>
    ///                 If summarization succeeds (or fails gracefully returning null), a <see cref="GenAIEvent" /> is
    ///                 published and the message is Acked.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Transient Failures</term>
    ///             <description>
    ///                 <see cref="HttpRequestException" /> triggers a Nack with requeue=true to retry the operation
    ///                 later.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>Fatal Errors</term>
    ///             <description>
    ///                 Any other exception logs a fatal error, publishes a failure <see cref="GenAIEvent" />, and
    ///                 Nacks the message with requeue=false (discard).
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    internal async Task HandleCommandAsync(GenAICommand command, IRabbitMqConsumer<GenAICommand> consumer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            GenAIWorkerLog.EmptyText(_logger, command.DocumentId);
            await consumer.AckAsync().ConfigureAwait(false);
            return;
        }

        GenAIWorkerLog.GeneratingSummary(_logger, command.DocumentId);

        try
        {
            var summary = await _summarizer.SummarizeAsync(command.Text, ct).ConfigureAwait(false);

            var resultEvent = !string.IsNullOrWhiteSpace(summary)
                ? new GenAIEvent(command.DocumentId, summary, DateTimeOffset.UtcNow)
                : new GenAIEvent(command.DocumentId, Summary: null, DateTimeOffset.UtcNow, "Failed to generate summary");

            await PublishResultAsync(resultEvent).ConfigureAwait(false);
            await consumer.AckAsync().ConfigureAwait(false);

            GenAIWorkerLog.Processed(_logger, command.DocumentId, summary is not null);
        }
        catch (HttpRequestException ex)
        {
            GenAIWorkerLog.TransientFailure(_logger, ex, command.DocumentId);
            await consumer.NackAsync().ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            GenAIWorkerLog.FatalFailure(_logger, ex, command.DocumentId);

            var failureEvent = new GenAIEvent(command.DocumentId, Summary: null, DateTimeOffset.UtcNow, ex.Message);
            try
            {
                await PublishResultAsync(failureEvent).ConfigureAwait(false);
            }
            catch (Exception pubEx)
            {
                GenAIWorkerLog.PublishFailureEventFailed(_logger, pubEx, command.DocumentId);
            }

            await consumer.NackAsync(requeue: false).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Asynchronously publishes a GenAI processing result event to the message queue using the configured routing key.
    /// </summary>
    /// <param name="genAiEvent">
    ///     The <see cref="GenAIEvent"/> payload to publish.
    ///     <para>Contains the original document ID, the generated summary (if successful), and any error messages.</para>
    /// </param>
    /// <returns>
    ///     A <see cref="Task"/> representing the asynchronous publish operation.
    /// </returns>
    /// <remarks>
    ///     Uses <see cref="RabbitMqSchema.GenAIEventRouting"/> as the routing key for downstream consumers.
    /// </remarks>
    private async Task PublishResultAsync(GenAIEvent genAiEvent) => await _publisher.PublishAsync(RabbitMqSchema.GenAIEventRouting, genAiEvent).ConfigureAwait(false);

    internal static partial class GenAIWorkerLog
    {
        [LoggerMessage(EventId = 3001, Level = LogLevel.Warning,
            Message = "Received GenAI command for document {DocumentId} with empty text")]
        public static partial void EmptyText(ILogger logger, Guid documentId);

        [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
            Message = "Generating summary for document {DocumentId}")]
        public static partial void GeneratingSummary(ILogger logger, Guid documentId);

        [LoggerMessage(EventId = 3003, Level = LogLevel.Information,
            Message = "Successfully processed document {DocumentId} - Summary: {HasSummary}")]
        public static partial void Processed(ILogger logger, Guid documentId, bool hasSummary);

        [LoggerMessage(EventId = 3004, Level = LogLevel.Warning,
            Message = "Transient GenAI failure for document {DocumentId}, requeueing")]
        public static partial void TransientFailure(ILogger logger, Exception exception, Guid documentId);

        [LoggerMessage(EventId = 3005, Level = LogLevel.Error,
            Message = "Fatal GenAI failure for document {DocumentId}, discarding")]
        public static partial void FatalFailure(ILogger logger, Exception exception, Guid documentId);

        [LoggerMessage(EventId = 3006, Level = LogLevel.Error,
            Message = "Failed to publish failure event for document {DocumentId}")]
        public static partial void PublishFailureEventFailed(ILogger logger, Exception exception, Guid documentId);
    }
}
