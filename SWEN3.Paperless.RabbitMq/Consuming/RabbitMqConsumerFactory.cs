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
        var channel = await _connection.CreateChannelAsync();
        await channel.BasicQosAsync(0, 1, false);
        return new RabbitMqConsumer<T>(channel, queueName);
    }
}