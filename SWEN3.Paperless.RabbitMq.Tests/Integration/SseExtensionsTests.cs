namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class SseExtensionsTests
{
    private const string TestEndpoint = "/sse-test";

    [Fact]
    public async Task MapSse_WithPublishedEvent_ShouldStreamToClient()
    {
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: null,
            configureEndpoints: endpoints => endpoints.MapSse<Messages.SseTestEvent>(
                TestEndpoint, evt => new { evt.Id, evt.Message }, _ => "test-event"));

        using var _ = host;
        var sseStream = host.Services.GetRequiredService<ISseStream<Messages.SseTestEvent>>();

        var clientId = Guid.NewGuid();
        var reader = sseStream.Subscribe(clientId);

        var testEvent = new Messages.SseTestEvent { Id = 42, Message = "Hello SSE" };
        sseStream.Publish(testEvent);

        var result = await reader.ReadAsync(TestContext.Current.CancellationToken);

        result.Id.Should().Be(42);
        result.Message.Should().Be("Hello SSE");

        sseStream.Unsubscribe(clientId);
    }

    [Fact]
    public async Task MapSse_MultipleClients_ShouldReceiveSameEvent()
    {
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: null,
            configureEndpoints: endpoints => endpoints.MapSse<Messages.SseTestEvent>(
                TestEndpoint, evt => new { evt.Id, evt.Message }, _ => "test-event"));

        using var _ = host;
        var sseStream = host.Services.GetRequiredService<ISseStream<Messages.SseTestEvent>>();

        var clientId1 = Guid.NewGuid();
        var clientId2 = Guid.NewGuid();
        var reader1 = sseStream.Subscribe(clientId1);
        var reader2 = sseStream.Subscribe(clientId2);

        var readTask1 = reader1.ReadAsync(TestContext.Current.CancellationToken).AsTask();
        var readTask2 = reader2.ReadAsync(TestContext.Current.CancellationToken).AsTask();

        var testEvent = new Messages.SseTestEvent { Id = 99, Message = "Broadcast" };
        sseStream.Publish(testEvent);

        var result1 = await readTask1;
        var result2 = await readTask2;

        result1.Should().BeEquivalentTo(testEvent);
        result2.Should().BeEquivalentTo(testEvent);

        sseStream.Unsubscribe(clientId1);
        sseStream.Unsubscribe(clientId2);
    }
}
