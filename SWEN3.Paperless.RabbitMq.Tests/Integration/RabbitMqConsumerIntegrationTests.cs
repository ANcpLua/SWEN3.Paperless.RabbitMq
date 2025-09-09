namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class RabbitMqConsumerIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();
    private IServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMQ:Uri"] = _container.GetConnectionString()
        }).Build();

        services.AddPaperlessRabbitMq(configuration);
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_WithMultipleMessages_ShouldProcessInOrder()
    {
        var factory = _serviceProvider.GetRequiredService<IRabbitMqConsumerFactory>();
        var connection = _serviceProvider.GetRequiredService<IConnection>();

        await using (var channel =
                     await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            await channel.QueueDeclareAsync("SimpleMessageQueue", true, false, false,
                cancellationToken: TestContext.Current.CancellationToken);
            await channel.QueuePurgeAsync("SimpleMessageQueue", TestContext.Current.CancellationToken);

            for (var i = 1; i <= 3; i++)
            {
                var message = JsonSerializer.Serialize(new Messages.SimpleMessage(i));
                await channel.BasicPublishAsync("", "SimpleMessageQueue", Encoding.UTF8.GetBytes(message),
                    TestContext.Current.CancellationToken);
            }
        }

        var consumer = await factory.CreateConsumerAsync<Messages.SimpleMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var messages = new List<Messages.SimpleMessage>();

        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(message);
            await consumer.AckAsync();
            if (messages.Count >= 3) break;
        }

        messages.Should().HaveCount(3);
        messages.Select(m => m.Id).Should().BeEquivalentTo([1, 2, 3]);
        await consumer.DisposeAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NackAsync_WithRequeue_ShouldHandleCorrectly(bool requeue)
    {
        var factory = _serviceProvider.GetRequiredService<IRabbitMqConsumerFactory>();
        var connection = _serviceProvider.GetRequiredService<IConnection>();

        await using (var channel =
                     await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            await channel.QueueDeclareAsync("SimpleMessageQueue", true, false, false,
                cancellationToken: TestContext.Current.CancellationToken);
            await channel.QueuePurgeAsync("SimpleMessageQueue", TestContext.Current.CancellationToken);
            var message = JsonSerializer.Serialize(new Messages.SimpleMessage(1));
            await channel.BasicPublishAsync("", "SimpleMessageQueue", Encoding.UTF8.GetBytes(message),
                TestContext.Current.CancellationToken);
        }

        var consumer = await factory.CreateConsumerAsync<Messages.SimpleMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        await enumerator.MoveNextAsync();

        await consumer.NackAsync(requeue);

        await enumerator.DisposeAsync();
        await consumer.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_WhenTokenCancelledWhileWaiting_ShouldCompleteEnumeration()
    {
        var factory = _serviceProvider.GetRequiredService<IRabbitMqConsumerFactory>();
        var connection = _serviceProvider.GetRequiredService<IConnection>();

        await using (var channel =
                     await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            await channel.QueueDeclareAsync("SimpleMessageQueue", true, false, false,
                cancellationToken: TestContext.Current.CancellationToken);
            await channel.QueuePurgeAsync("SimpleMessageQueue", TestContext.Current.CancellationToken);
            var message = JsonSerializer.Serialize(new Messages.SimpleMessage(1));
            await channel.BasicPublishAsync("", "SimpleMessageQueue", Encoding.UTF8.GetBytes(message),
                TestContext.Current.CancellationToken);
        }

        var consumer = await factory.CreateConsumerAsync<Messages.SimpleMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        await enumerator.MoveNextAsync();

        await cts.CancelAsync();
        await enumerator.DisposeAsync();
        await consumer.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_WithCancelledToken_ShouldThrowTaskCanceledException()
    {
        var factory = _serviceProvider.GetRequiredService<IRabbitMqConsumerFactory>();
        var consumer = await factory.CreateConsumerAsync<Messages.SimpleMessage>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            await enumerator.MoveNextAsync();
        });

        await consumer.DisposeAsync();
    }

    [Fact]
    public async Task ConsumeAsync_WhenTokenCancelledAfterMessage_ShouldCompleteGracefully()
    {
        var factory = _serviceProvider.GetRequiredService<IRabbitMqConsumerFactory>();
        var connection = _serviceProvider.GetRequiredService<IConnection>();

        await using (var channel =
                     await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            await channel.QueueDeclareAsync("SimpleMessageQueue", true, false, false,
                cancellationToken: TestContext.Current.CancellationToken);
            await channel.QueuePurgeAsync("SimpleMessageQueue", TestContext.Current.CancellationToken);

            var message = JsonSerializer.Serialize(new Messages.SimpleMessage(1));
            await channel.BasicPublishAsync("", "SimpleMessageQueue", Encoding.UTF8.GetBytes(message),
                TestContext.Current.CancellationToken);
        }

        var consumer = await factory.CreateConsumerAsync<Messages.SimpleMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        await enumerator.MoveNextAsync();

        await cts.CancelAsync();

        await enumerator.DisposeAsync();
        await consumer.DisposeAsync();
    }
}