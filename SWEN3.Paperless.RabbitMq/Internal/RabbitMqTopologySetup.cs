using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SWEN3.Paperless.RabbitMq.Schema;

namespace SWEN3.Paperless.RabbitMq.Internal;

internal class RabbitMqTopologySetup : IHostedService
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqTopologySetup> _logger;

    public RabbitMqTopologySetup(IConnection connection, ILogger<RabbitMqTopologySetup> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting up RabbitMQ topology...");

        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await channel.ExchangeDeclareAsync(RabbitMqSchema.Exchange, ExchangeType.Topic, durable: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(RabbitMqSchema.OcrCommandQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(RabbitMqSchema.OcrEventQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(RabbitMqSchema.GenAICommandQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(RabbitMqSchema.GenAIEventQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(RabbitMqSchema.OcrCommandQueue, RabbitMqSchema.Exchange,
            RabbitMqSchema.OcrCommandRouting, cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(RabbitMqSchema.OcrEventQueue, RabbitMqSchema.Exchange,
            RabbitMqSchema.OcrEventRouting, cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(RabbitMqSchema.GenAICommandQueue, RabbitMqSchema.Exchange,
            RabbitMqSchema.GenAICommandRouting, cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(RabbitMqSchema.GenAIEventQueue, RabbitMqSchema.Exchange,
            RabbitMqSchema.GenAIEventRouting, cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("RabbitMQ topology setup completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
