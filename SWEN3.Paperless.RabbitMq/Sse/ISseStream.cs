using System.Threading.Channels;

namespace SWEN3.Paperless.RabbitMq.Sse;

/// <summary>
///     Interface for Server-Sent Events streaming.
///     Manages subscriptions and event distribution to connected clients.
/// </summary>
/// <typeparam name="T">The type of events to stream.</typeparam>
public interface ISseStream<T>
{
    /// <summary>
    ///     Subscribes a client to the event stream.
    /// </summary>
    /// <param name="clientId">Unique identifier for the client.</param>
    /// <returns>A channel reader for receiving events.</returns>
    ChannelReader<T> Subscribe(Guid clientId);

    /// <summary>
    ///     Unsubscribes a client from the event stream.
    ///     Closes the client's channel and prevents further event delivery.
    /// </summary>
    /// <param name="clientId">The client identifier to unsubscribe.</param>
    void Unsubscribe(Guid clientId);

    /// <summary>
    ///     Publishes an event to all subscribed clients.
    ///     The event is delivered asynchronously to all active subscriptions.
    /// </summary>
    /// <param name="item">The event to publish.</param>
    void Publish(T item);
}