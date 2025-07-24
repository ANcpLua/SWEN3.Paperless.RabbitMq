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

        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            RabbitMqSchema.Exchange,
            ExchangeType.Topic,
            true,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            RabbitMqSchema.OcrCommandQueue,
            true,
            false,
            false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            RabbitMqSchema.OcrEventQueue,
            true,
            false,
            false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RabbitMqSchema.OcrCommandQueue,
            RabbitMqSchema.Exchange,
            RabbitMqSchema.OcrCommandRouting,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RabbitMqSchema.OcrEventQueue,
            RabbitMqSchema.Exchange,
            RabbitMqSchema.OcrEventRouting,
            cancellationToken: cancellationToken);

        _logger.LogInformation("RabbitMQ topology setup completed");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}