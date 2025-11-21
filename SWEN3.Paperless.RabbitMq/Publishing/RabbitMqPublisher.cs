using System.Text.Json;
using RabbitMQ.Client;
using SWEN3.Paperless.RabbitMq.Schema;

namespace SWEN3.Paperless.RabbitMq.Publishing;

internal class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IConnection _connection;

    public RabbitMqPublisher(IConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishAsync<T>(string routingKey, T message) where T : class
    {
        var cancellation = _connection.CloseReason?.CancellationToken ?? CancellationToken.None;
        await using var channel =
            await _connection.CreateChannelAsync(cancellationToken: cancellation).ConfigureAwait(false);
        var channelCancellation = channel.CloseReason?.CancellationToken ?? CancellationToken.None;
        await channel.BasicPublishAsync(
            RabbitMqSchema.Exchange,
            routingKey,
            JsonSerializer.SerializeToUtf8Bytes(message),
            channelCancellation).ConfigureAwait(false);
    }
}
