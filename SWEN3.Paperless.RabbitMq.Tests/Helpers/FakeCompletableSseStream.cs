using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

/// <summary>
///     Test-only implementation of ISseStream that mirrors production semantics:
///     one channel per subscriber plus an explicit Complete() hook to end streams naturally.
/// </summary>
internal sealed class FakeCompletableSseStream<T> : ISseStream<T> where T : class
{
    private volatile bool _disposed;
    private readonly ConcurrentDictionary<Guid, Channel<T>> _channels = new();

    /// <summary>
    ///     Gets the current number of active subscribers.
    /// </summary>
    public int ClientCount => _channels.Count;

    /// <summary>
    ///     Publishes an event to all subscribers.
    /// </summary>
    public void Publish(T message)
    {
        if (_disposed)
            return;

        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryWrite(message);
        }
    }

    /// <summary>
    ///     Subscribes a client and returns a channel reader for receiving events.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the stream has been disposed.</exception>
    public ChannelReader<T> Subscribe(Guid clientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _channels[clientId] = channel;
        return channel.Reader;
    }

    /// <summary>
    ///     Unsubscribes a client.
    /// </summary>
    public void Unsubscribe(Guid clientId)
    {
        if (_channels.TryRemove(clientId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    ///     Completes the stream, causing ReadAllAsync to terminate naturally.
    /// </summary>
    public void Complete()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    ///     Disposes the stream, completing all client channels.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        Complete();
        _channels.Clear();
        return ValueTask.CompletedTask;
    }
}
