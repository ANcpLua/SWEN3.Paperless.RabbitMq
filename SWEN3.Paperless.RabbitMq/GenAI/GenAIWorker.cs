using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SWEN3.Paperless.RabbitMq.Consuming;
using SWEN3.Paperless.RabbitMq.Models;
using SWEN3.Paperless.RabbitMq.Publishing;
using SWEN3.Paperless.RabbitMq.Schema;

namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Background service that processes GenAI summarization commands from the message queue.
///     <para>Consumes <see cref="GenAICommand"/> messages, generates document summaries using <see cref="ITextSummarizer"/>, 
///     and publishes <see cref="GenAIEvent"/> results back to the queue for downstream processing.</para>
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
public sealed class GenAIWorker : BackgroundService
{
    private readonly IRabbitMqConsumerFactory _consumerFactory;
    private readonly ILogger<GenAIWorker> _logger;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ITextSummarizer _summarizer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenAIWorker"/> class.
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
            if (stoppingToken.IsCancellationRequested) break;

            await HandleCommandAsync(command, consumer, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("GenAIWorker stopped");
    }

    /// <summary>
    ///     Processes a single GenAI command by generating a summary and publishing the result.
    ///     Implements retry logic for transient failures and error event publishing for permanent failures.
    /// </summary>
    /// <param name="command">The GenAI command containing document text to summarize.</param>
    /// <param name="consumer">The consumer instance for message acknowledgment.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task HandleCommandAsync(GenAICommand command, IRabbitMqConsumer<GenAICommand> consumer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            _logger.LogWarning("Received GenAI command for document {DocumentId} with empty text", command.DocumentId);
            await consumer.AckAsync().ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("Generating summary for document {DocumentId}", command.DocumentId);

        try
        {
            var summary = await _summarizer.SummarizeAsync(command.Text, ct).ConfigureAwait(false);

            var resultEvent = !string.IsNullOrWhiteSpace(summary)
                ? new GenAIEvent(command.DocumentId, summary, DateTimeOffset.UtcNow)
                : new GenAIEvent(command.DocumentId, null, DateTimeOffset.UtcNow, "Failed to generate summary");

            await PublishResultAsync(resultEvent).ConfigureAwait(false);
            await consumer.AckAsync().ConfigureAwait(false);

            _logger.LogInformation("Successfully processed document {DocumentId} - Summary: {HasSummary}",
                command.DocumentId, summary is not null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Transient GenAI failure for document {DocumentId}, requeueing", command.DocumentId);
            await consumer.NackAsync().ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal GenAI failure for document {DocumentId}, discarding", command.DocumentId);

            var failureEvent = new GenAIEvent(command.DocumentId, null, DateTimeOffset.UtcNow, ex.Message);
            try
            {
                await PublishResultAsync(failureEvent).ConfigureAwait(false);
            }
            catch (Exception pubEx)
            {
                _logger.LogError(pubEx, "Failed to publish failure event for document {DocumentId}",
                    command.DocumentId);
            }

            await consumer.NackAsync(false).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Publishes a GenAI processing result event to the message queue for downstream consumption.
    /// </summary>
    /// <param name="genAiEvent">The GenAI event containing the processing result or error information.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task PublishResultAsync(GenAIEvent genAiEvent)
    {
        await _publisher.PublishAsync(RabbitMqSchema.GenAIEventRouting, genAiEvent).ConfigureAwait(false);
    }
}