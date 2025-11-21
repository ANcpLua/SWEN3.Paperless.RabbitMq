using System.Threading.Channels;

namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

/// <summary>
///     Test-only implementation of ISseStream that can be completed on demand.
///     Uses a Channel internally to enable natural completion of ReadAllAsync.
/// </summary>
internal sealed class FakeCompletableSseStream<T> : ISseStream<T> where T : class
{
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();
    private readonly Dictionary<Guid, ChannelReader<T>> _subscribers = new();
    private readonly Lock _lock = new();

    /// <summary>
    ///     Gets the current number of active subscribers.
    /// </summary>
    public int ClientCount
    {
        get
        {
            lock (_lock)
            {
                return _subscribers.Count;
            }
        }
    }

    /// <summary>
    ///     Publishes an event to all subscribers.
    /// </summary>
    public void Publish(T message)
    {
        _channel.Writer.TryWrite(message);
    }

    /// <summary>
    ///     Subscribes a client and returns a channel reader for receiving events.
    /// </summary>
    public ChannelReader<T> Subscribe(Guid clientId)
    {
        lock (_lock)
        {
            _subscribers[clientId] = _channel.Reader;
            return _channel.Reader;
        }
    }

    /// <summary>
    ///     Unsubscribes a client.
    /// </summary>
    public void Unsubscribe(Guid clientId)
    {
        lock (_lock)
        {
            _subscribers.Remove(clientId);
        }
    }

    /// <summary>
    ///     Completes the stream, causing ReadAllAsync to terminate naturally.
    /// </summary>
    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
