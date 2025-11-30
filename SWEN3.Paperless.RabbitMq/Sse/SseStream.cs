using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SWEN3.Paperless.RabbitMq.Sse;

/// <summary>
///     Thread-safe in-memory SSE stream using bounded channels with DropOldest backpressure.
/// </summary>
/// <typeparam name="T">The type of event object being streamed.</typeparam>
internal sealed class SseStream<T> : ISseStream<T>
{
    private readonly ConcurrentDictionary<Guid, Channel<T>> _channels = new();
    private volatile bool _disposed;

    /// <inheritdoc />
    public int ClientCount => _channels.Count;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown if the stream has been disposed.</exception>
    public ChannelReader<T> Subscribe(Guid clientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        });

        _channels[clientId] = channel;
        return channel.Reader;
    }

    /// <inheritdoc />
    public void Unsubscribe(Guid clientId)
    {
        if (_channels.TryRemove(clientId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    /// <inheritdoc />
    public void Publish(T item)
    {
        if (_disposed)
            return;

        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryWrite(item);
        }
    }

    /// <summary>
    ///     Disposes the stream, completing all client channels for graceful shutdown.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;

        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryComplete();
        }

        _channels.Clear();
        return ValueTask.CompletedTask;
    }
}
