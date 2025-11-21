namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class GenAIIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = RabbitMqTestContainerFactory.Create();
    private IHost _host = null!;
    private IServiceProvider _serviceProvider = null!;

    private IRabbitMqPublisher Publisher => _serviceProvider.GetRequiredService<IRabbitMqPublisher>();
    private IRabbitMqConsumerFactory ConsumerFactory => _serviceProvider.GetRequiredService<IRabbitMqConsumerFactory>();
    private IConnection Connection => _serviceProvider.GetRequiredService<IConnection>();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var hostBuilder = Host.CreateDefaultBuilder().ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Uri"] = _container.GetConnectionString()
            });
        }).ConfigureServices((context, services) =>
        {
            services.AddPaperlessRabbitMq(context.Configuration);
            services.AddLogging();
        });
        _host = hostBuilder.Build();
        _serviceProvider = _host.Services;
        await _host.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task GenAIEvent_ShouldPublishAndConsume_Successfully()
    {
        var testEvent = new GenAIEvent(Guid.NewGuid(), "This is a test summary for integration testing",
            DateTimeOffset.UtcNow);
        await Publisher.PublishGenAIEventAsync(testEvent);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        GenAIEvent? receivedEvent = null;
        await using var consumer = await ConsumerFactory.CreateConsumerAsync<GenAIEvent>();
        await using var enumerator = consumer.ConsumeAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        if (await enumerator.MoveNextAsync())
        {
            receivedEvent = enumerator.Current;
            await consumer.AckAsync();
        }

        receivedEvent.Should().NotBeNull();
        receivedEvent.DocumentId.Should().Be(testEvent.DocumentId);
        receivedEvent.Summary.Should().Be(testEvent.Summary);
        receivedEvent.GeneratedAt.Should().BeCloseTo(testEvent.GeneratedAt, TimeSpan.FromSeconds(1));
        receivedEvent.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GenAIEvent_WithError_ShouldPublishAndConsume_Successfully()
    {
        var testEvent = new GenAIEvent(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow,
            "Failed to generate summary: API rate limit exceeded");
        await Publisher.PublishGenAIEventAsync(testEvent);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        GenAIEvent? receivedEvent = null;
        await using var consumer = await ConsumerFactory.CreateConsumerAsync<GenAIEvent>();
        await using var enumerator = consumer.ConsumeAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        if (await enumerator.MoveNextAsync())
        {
            receivedEvent = enumerator.Current;
            await consumer.AckAsync();
        }

        receivedEvent.Should().NotBeNull();
        receivedEvent.DocumentId.Should().Be(testEvent.DocumentId);
        receivedEvent.Summary.Should().BeEmpty();
        receivedEvent.ErrorMessage.Should().Be(testEvent.ErrorMessage);
    }

    [Fact]
    public async Task GenAIEvent_MultipleEvents_ShouldProcessInOrder()
    {
        var events = Enumerable.Range(1, 3).Select(i => new GenAIEvent(Guid.NewGuid(),
            $"Summary {i}", DateTimeOffset.UtcNow.AddMinutes(i))).ToList();

        await using var consumer = await ConsumerFactory.CreateConsumerAsync<GenAIEvent>();
        foreach (var evt in events)
            await Publisher.PublishGenAIEventAsync(evt);

        var receivedEvents = new List<GenAIEvent>();
        await foreach (var message in consumer.ConsumeAsync(TestContext.Current.CancellationToken))
        {
            receivedEvents.Add(message);
            await consumer.AckAsync();
            if (receivedEvents.Count >= 3)
                break;
        }

        receivedEvents.Should().HaveCount(3);
        receivedEvents.Should()
            .BeEquivalentTo(events, options => options.WithStrictOrdering().ComparingByMembers<GenAIEvent>());
    }

    [Fact]
    public async Task GenAIQueue_ShouldExistInTopology()
    {
        await using var channel =
            await Connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await channel.QueueDeclarePassiveAsync(RabbitMqSchema.GenAIEventQueue,
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.QueueName.Should().Be(RabbitMqSchema.GenAIEventQueue);
    }

    [Fact]
    public async Task GenAIEvent_Nack_ShouldRequeue()
    {
        var testEvent = new GenAIEvent(Guid.NewGuid(), "Nack test", DateTimeOffset.UtcNow);
        await Publisher.PublishGenAIEventAsync(testEvent);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        await using (var consumer1 = await ConsumerFactory.CreateConsumerAsync<GenAIEvent>())
        {
            await using var enumerator1 = consumer1.ConsumeAsync(TestContext.Current.CancellationToken)
                .GetAsyncEnumerator(TestContext.Current.CancellationToken);
            if (await enumerator1.MoveNextAsync())
            {
                await consumer1.NackAsync();
            }
        }

        GenAIEvent? redeliveredEvent = null;
        await using (var consumer2 = await ConsumerFactory.CreateConsumerAsync<GenAIEvent>())
        {
            await using var enumerator2 = consumer2.ConsumeAsync(TestContext.Current.CancellationToken)
                .GetAsyncEnumerator(TestContext.Current.CancellationToken);
            if (await enumerator2.MoveNextAsync())
            {
                redeliveredEvent = enumerator2.Current;
                await consumer2.AckAsync();
            }
        }

        redeliveredEvent.Should().NotBeNull();
        redeliveredEvent.DocumentId.Should().Be(testEvent.DocumentId);
        redeliveredEvent.Summary.Should().Be(testEvent.Summary);
    }
}
