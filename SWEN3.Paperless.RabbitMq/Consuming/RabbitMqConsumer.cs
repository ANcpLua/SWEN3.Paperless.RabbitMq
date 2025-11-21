using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SWEN3.Paperless.RabbitMq.Internal;

namespace SWEN3.Paperless.RabbitMq.Consuming;

internal class RabbitMqConsumer<T> : IRabbitMqConsumer<T> where T : class
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private ulong? _currentDeliveryTag;

    public RabbitMqConsumer(IChannel channel, string queueName)
    {
        _channel = channel;
        _queueName = queueName;
    }

    [ExcludeFromCodeCoverage]
    public async IAsyncEnumerable<T> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        var messageChannel = Channel.CreateUnbounded<(T message, ulong deliveryTag)>();

        consumer.ReceivedAsync += async (_, ea) =>
            await ProcessMessageAsync(ea, messageChannel.Writer, cancellationToken).ConfigureAwait(false);

        await _channel.BasicConsumeAsync(_queueName, autoAck: false, consumer, cancellationToken).ConfigureAwait(false);

        await foreach (var (message, deliveryTag) in messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            _currentDeliveryTag = deliveryTag;
            yield return message;
        }
    }

    public async Task AckAsync()
    {
        if (_currentDeliveryTag.HasValue)
        {
            var cancellation = _channel.CloseReason?.CancellationToken ?? CancellationToken.None;
            await _channel.BasicAckAsync(_currentDeliveryTag.Value, multiple: false, cancellation)
                .ConfigureAwait(false);
            _currentDeliveryTag = null;
        }
    }

    public async Task NackAsync(bool requeue = true)
    {
        if (_currentDeliveryTag.HasValue)
        {
            var cancellation = _channel.CloseReason?.CancellationToken ?? CancellationToken.None;
            await _channel.BasicNackAsync(_currentDeliveryTag.Value, multiple: false, requeue, cancellation)
                .ConfigureAwait(false);
            _currentDeliveryTag = null;
        }
    }

    public async ValueTask DisposeAsync() => await _channel.DisposeAsync().ConfigureAwait(false);

    internal async Task ProcessMessageAsync(BasicDeliverEventArgs ea,
        ChannelWriter<(T message, ulong deliveryTag)> writer, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<T>(ea.Body.Span, RabbitMqJsonOptions.Options);

            if (message is not null)
                await writer.WriteAsync((message, ea.DeliveryTag), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
