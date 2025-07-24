namespace SWEN3.Paperless.RabbitMq.Consuming;

/// <summary>
///     RabbitMQ message consumer interface for processing messages from a queue.
/// </summary>
/// <typeparam name="T">The message type to consume.</typeparam>
public interface IRabbitMqConsumer<out T> : IAsyncDisposable where T : class
{
    /// <summary>
    ///     Consumes messages from the queue asynchronously.
    ///     Messages must be acknowledged or rejected after processing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop consumption.</param>
    /// <returns>An async enumerable of consumed messages.</returns>
    /// <example>
    ///     <code>
    /// await foreach (var message in consumer.ConsumeAsync(stoppingToken))
    /// {
    ///     // Process message
    ///     await consumer.AckAsync();
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<T> ConsumeAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Acknowledges the current message, removing it from the queue permanently.
    ///     Must be called after successfully processing a message.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AckAsync();

    /// <summary>
    ///     Negatively acknowledges the current message, indicating processing failure.
    /// </summary>
    /// <param name="requeue">Whether to requeue the message for retry. Default is true.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    ///     <code>
    ///  await consumer.NackAsync(requeue: true);
    /// </code>
    /// </example>
    Task NackAsync(bool requeue = true);
}