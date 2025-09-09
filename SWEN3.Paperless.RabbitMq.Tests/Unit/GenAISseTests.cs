namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GenAISseTests
{
    [Fact]
    public void AddSseStream_ForGenAIEvent_ShouldRegisterService()
    {
        var services = new ServiceCollection();
        services.AddSseStream<GenAIEvent>();
        var provider = services.BuildServiceProvider();
        var sseStream = provider.GetService<ISseStream<GenAIEvent>>();
        sseStream.Should().NotBeNull();
        sseStream.Should().BeOfType<SseStream<GenAIEvent>>();
    }

    [Fact]
    public async Task SseStream_ShouldPublishGenAIEventToSubscribers()
    {
        var services = new ServiceCollection();
        services.AddSseStream<GenAIEvent>();
        var provider = services.BuildServiceProvider();
        var sseStream = provider.GetRequiredService<ISseStream<GenAIEvent>>();
        var clientId = Guid.NewGuid();
        var reader = sseStream.Subscribe(clientId);
        var genAiEvent = new GenAIEvent(Guid.NewGuid(), "Test summary", DateTimeOffset.UtcNow);
        sseStream.Publish(genAiEvent);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var receivedEvent = await reader.ReadAsync(cts.Token);
        receivedEvent.Should().NotBeNull();
        receivedEvent.DocumentId.Should().Be(genAiEvent.DocumentId);
        receivedEvent.Summary.Should().Be(genAiEvent.Summary);
    }

    [Fact]
    public async Task SseStream_MultipleSubscribers_ShouldAllReceiveGenAIEvent()
    {
        var services = new ServiceCollection();
        services.AddSseStream<GenAIEvent>();
        var provider = services.BuildServiceProvider();
        var sseStream = provider.GetRequiredService<ISseStream<GenAIEvent>>();
        var client1 = Guid.NewGuid();
        var client2 = Guid.NewGuid();
        var reader1 = sseStream.Subscribe(client1);
        var reader2 = sseStream.Subscribe(client2);
        var genAiEvent = new GenAIEvent(Guid.NewGuid(), "Broadcast test", DateTimeOffset.UtcNow);
        sseStream.Publish(genAiEvent);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var received1 = await reader1.ReadAsync(cts.Token);
        var received2 = await reader2.ReadAsync(cts.Token);
        received1.Should().NotBeNull();
        received2.Should().NotBeNull();
        received1.DocumentId.Should().Be(genAiEvent.DocumentId);
        received2.DocumentId.Should().Be(genAiEvent.DocumentId);
    }
}