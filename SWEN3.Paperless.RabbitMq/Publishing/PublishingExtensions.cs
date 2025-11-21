using SWEN3.Paperless.RabbitMq.Schema;

namespace SWEN3.Paperless.RabbitMq.Publishing;

/// <summary>
///     Provides Extension methods to publish OCR commands and events to the message queue.
///     <para>Use <see cref="PublishingExtensions.PublishOcrCommandAsync{T}" /> to publish commands</para>
///     <para>and <see cref="PublishingExtensions.PublishOcrEventAsync{T}" /> to publish events.</para>
/// </summary>
public static class PublishingExtensions
{
    /// <summary>
    ///     Publishes an OCR command to the message queue.
    /// </summary>
    /// <typeparam name="T">The command type. Must be a class.</typeparam>
    /// <param name="publisher">The RabbitMQ publisher instance.</param>
    /// <param name="command">The command to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    ///     <code>var command = new OcrCommand(doc.Id, doc.FileName, doc.StoragePath);</code>
    ///     <code>await publisher.PublishOcrCommandAsync(command);</code>
    /// </example>
    public static async Task PublishOcrCommandAsync<T>(this IRabbitMqPublisher publisher, T command) where T : class =>
        await publisher.PublishAsync(RabbitMqSchema.OcrCommandRouting, command).ConfigureAwait(false);

    /// <summary>
    ///     Publishes an OCR event to the message queue.
    /// </summary>
    /// <typeparam name="T">The event type. Must be a class.</typeparam>
    /// <param name="publisher">The RabbitMQ publisher instance.</param>
    /// <param name="event">The event to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    ///     Success case:
    ///     <code>var result = new OcrEvent(request.JobId, "Completed", text, DateTimeOffset.UtcNow);</code>
    ///     <code>await publisher.PublishOcrEventAsync(result);</code>
    ///     <para />
    ///     Failure case:
    ///     <code>var result = new OcrEvent(request.JobId, "Failed", ex.Message, DateTimeOffset.UtcNow);</code>
    ///     <code>await publisher.PublishOcrEventAsync(result);</code>
    /// </example>
    public static async Task PublishOcrEventAsync<T>(this IRabbitMqPublisher publisher, T @event) where T : class =>
        await publisher.PublishAsync(RabbitMqSchema.OcrEventRouting, @event).ConfigureAwait(false);
}
