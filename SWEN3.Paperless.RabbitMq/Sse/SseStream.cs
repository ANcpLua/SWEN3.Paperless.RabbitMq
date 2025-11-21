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

    /// <inheritdoc />
    public int ClientCount => _channels.Count;

    public ChannelReader<T> Subscribe(Guid clientId)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true
        });

        _channels[clientId] = channel;
        return channel.Reader;
    }

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
        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryWrite(item);
        }
    }
}
