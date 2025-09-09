using SWEN3.Paperless.RabbitMq.Schema;

namespace SWEN3.Paperless.RabbitMq.Publishing;

/// <summary>
///     Provides extension methods to publish GenAI commands and events to the message queue.
///     <para>Use <see cref="GenAIPublishingExtensions.PublishGenAICommandAsync{T}" /> to publish GenAI summarization commands.</para>
///     <para>Use <see cref="GenAIPublishingExtensions.PublishGenAIEventAsync{T}" /> to publish GenAI processing results.</para>
/// </summary>
public static class GenAIPublishingExtensions
{
    /// <summary>
    ///     Publishes a GenAI command to the message queue.
    /// </summary>
    /// <typeparam name="T">The command type. Must be a class.</typeparam>
    /// <param name="publisher">The RabbitMQ publisher instance.</param>
    /// <param name="command">The GenAI command to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task PublishGenAICommandAsync<T>(this IRabbitMqPublisher publisher, T command) where T : class
    {
        await publisher.PublishAsync(RabbitMqSchema.GenAICommandRouting, command);
    }

    /// <summary>
    ///     Publishes a GenAI event to the message queue.
    /// </summary>
    /// <typeparam name="T">The event type. Must be a class.</typeparam>
    /// <param name="publisher">The RabbitMQ publisher instance.</param>
    /// <param name="event">The GenAI event to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    ///     Success case:
    ///     <code>
    /// var result = new GenAIEvent(doc.Id, summary, DateTimeOffset.UtcNow);
    /// await publisher.PublishGenAIEventAsync(result);
    ///     </code>
    ///     <para />
    ///     Failure case (include an error message when summary could not be generated):
    ///     <code>
    /// var result = new GenAIEvent(doc.Id, string.Empty, DateTimeOffset.UtcNow, ex.Message);
    /// await publisher.PublishGenAIEventAsync(result);
    ///     </code>
    /// </example>
    public static async Task PublishGenAIEventAsync<T>(this IRabbitMqPublisher publisher, T @event) where T : class
    {
        await publisher.PublishAsync(RabbitMqSchema.GenAIEventRouting, @event);
    }
}