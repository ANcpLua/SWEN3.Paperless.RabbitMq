using System.Text.Json;

namespace SWEN3.Paperless.RabbitMq.Publishing;

/// <summary>
///     Interface for publishing messages to RabbitMQ exchanges.
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>
    ///     Publishes a message to the specified routing key.
    ///     Messages are serialized to JSON using System.Text.Json.
    /// </summary>
    /// <typeparam name="T">The message type. Must be a class.</typeparam>
    /// <param name="routingKey">The routing key for message delivery.</param>
    /// <param name="message">The message to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="JsonException">Thrown when message serialization fails.</exception>
    Task PublishAsync<T>(string routingKey, T message) where T : class;
}