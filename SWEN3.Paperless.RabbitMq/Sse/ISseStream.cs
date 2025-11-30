using System.Threading.Channels;

namespace SWEN3.Paperless.RabbitMq.Sse;

/// <summary>
///     Defines a contract for a service that manages Server-Sent Events (SSE) streaming.
///     <para>
///         Handles multiple client subscriptions, asynchronous event distribution, and lifecycle management
///         (subscribing/unsubscribing) for real-time updates.
///     </para>
/// </summary>
/// <typeparam name="T">
///     The type of event object being streamed.
/// </typeparam>
public interface ISseStream<T> : IAsyncDisposable
{
    /// <summary>
    ///     Gets the number of currently active client subscriptions.
    /// </summary>
    int ClientCount { get; }

    /// <summary>
    ///     Registers a new client to receive events from the stream.
    /// </summary>
    /// <param name="clientId">
    ///     A unique identifier for the client session.
    /// </param>
    /// <returns>
    ///     A <see cref="ChannelReader{T}"/> that the client can read from to receive asynchronous event updates.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown if the stream has been disposed.</exception>
    ChannelReader<T> Subscribe(Guid clientId);

    /// <summary>
    ///     Removes a client's subscription, closing their communication channel.
    /// </summary>
    /// <param name="clientId">
    ///     The unique identifier of the client to unsubscribe.
    /// </param>
    void Unsubscribe(Guid clientId);

    /// <summary>
    ///     Broadcasts an event to all currently subscribed clients.
    /// </summary>
    /// <param name="item">
    ///     The event object to publish.
    /// </param>
    /// <remarks>
    ///     The event is written to each client's individual channel. If a client's channel is full or closed,
    ///     the delivery strategy (e.g., dropping the message) is determined by the implementation.
    ///     If the stream has been disposed, this method is a no-op.
    /// </remarks>
    void Publish(T item);
}
