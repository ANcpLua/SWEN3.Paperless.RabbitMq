namespace SWEN3.Paperless.RabbitMq.Consuming;

/// <summary>
///     Factory for creating RabbitMQ consumers.
/// </summary>
public interface IRabbitMqConsumerFactory
{
    /// <summary>
    ///     Creates a new consumer for the specified message type.
    ///     The queue name is derived from the type name with "Queue" suffix.
    /// </summary>
    /// <typeparam name="T">The message type to consume. Must be a class.</typeparam>
    /// <returns>A configured consumer instance ready to consume messages.</returns>
    /// <example>
    ///     <code>
    /// await using var consumer = await factory.CreateConsumerAsync&lt; OcrEvent&gt;();
    /// </code>
    /// </example>
    Task<IRabbitMqConsumer<T>> CreateConsumerAsync<T>() where T : class;
}