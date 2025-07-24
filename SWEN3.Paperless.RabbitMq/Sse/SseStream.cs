using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SWEN3.Paperless.RabbitMq.Sse;

internal class SseStream<T> : ISseStream<T>
{
    private readonly ConcurrentDictionary<Guid, Channel<T>> _channels = new();

    public ChannelReader<T> Subscribe(Guid clientId)
    {
        var channel = Channel.CreateUnbounded<T>();
        _channels[clientId] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(Guid clientId)
    {
        if (_channels.TryRemove(clientId, out var channel)) channel.Writer.TryComplete();
    }

    public void Publish(T item)
    {
        foreach (var channel in _channels.Values) channel.Writer.TryWrite(item);
    }
}