using RabbitMQ.Client;

namespace SWEN3.Paperless.RabbitMq.Consuming;

internal class RabbitMqConsumerFactory : IRabbitMqConsumerFactory
{
    private readonly IConnection _connection;

    public RabbitMqConsumerFactory(IConnection connection)
    {
        _connection = connection;
    }

    public async Task<IRabbitMqConsumer<T>> CreateConsumerAsync<T>() where T : class
    {
        var queueName = typeof(T).Name + "Queue";
        var channel = await _connection
            .CreateChannelAsync(cancellationToken: _connection.CloseReason?.CancellationToken ?? CancellationToken.None)
            .ConfigureAwait(false);
        var cancellation = channel.CloseReason?.CancellationToken ?? CancellationToken.None;
        await channel.BasicQosAsync(0, 1, global: false, cancellation).ConfigureAwait(false);
        return new RabbitMqConsumer<T>(channel, queueName);
    }
}
