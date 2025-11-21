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
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        var ct = TestContext.Current.CancellationToken;

        var responseTask = client.GetAsync(TestEndpoint, HttpCompletionOption.ResponseHeadersRead, ct);

        // Wait for HTTP client to connect
        while (sseStream.ClientCount == 0)
            await Task.Delay(50, ct);

        var testEvent = new Messages.SseTestEvent { Id = 42, Message = "Hello SSE" };
        sseStream.Publish(testEvent);

        using var response = await responseTask;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var eventLine = await reader.ReadLineAsync(ct);
        var dataLine = await reader.ReadLineAsync(ct);
        var blankLine = await reader.ReadLineAsync(ct);

        eventLine.Should().Be("event: test-event");
        // Note: .NET 9 uses PascalCase, .NET 10+ uses camelCase
        dataLine.Should().Contain("42");
        dataLine.Should().Contain("Hello SSE");
        blankLine.Should().BeEmpty();
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
        var ct = TestContext.Current.CancellationToken;

        var client1 = server.CreateClient();
        var client2 = server.CreateClient();
        client1.Timeout = Timeout.InfiniteTimeSpan;
        client2.Timeout = Timeout.InfiniteTimeSpan;

        var responseTask1 = client1.GetAsync(TestEndpoint, HttpCompletionOption.ResponseHeadersRead, ct);
        var responseTask2 = client2.GetAsync(TestEndpoint, HttpCompletionOption.ResponseHeadersRead, ct);

        // Wait for both HTTP clients to connect
        while (sseStream.ClientCount < 2)
            await Task.Delay(50, ct);

        var testEvent = new Messages.SseTestEvent { Id = 99, Message = "Broadcast" };
        sseStream.Publish(testEvent);

        using var response1 = await responseTask1;
        using var response2 = await responseTask2;

        response1.EnsureSuccessStatusCode();
        response2.EnsureSuccessStatusCode();

        await using var stream1 = await response1.Content.ReadAsStreamAsync(ct);
        await using var stream2 = await response2.Content.ReadAsStreamAsync(ct);
        using var reader1 = new StreamReader(stream1, Encoding.UTF8);
        using var reader2 = new StreamReader(stream2, Encoding.UTF8);

        var event1 = await reader1.ReadLineAsync(ct);
        var data1 = await reader1.ReadLineAsync(ct);
        var blank1 = await reader1.ReadLineAsync(ct);

        var event2 = await reader2.ReadLineAsync(ct);
        var data2 = await reader2.ReadLineAsync(ct);
        var blank2 = await reader2.ReadLineAsync(ct);

        event1.Should().Be("event: test-event");
        // Note: .NET 9 uses PascalCase, .NET 10+ uses camelCase
        data1.Should().Contain("99");
        data1.Should().Contain("Broadcast");
        blank1.Should().BeEmpty();

        event2.Should().Be("event: test-event");
        data2.Should().Contain("99");
        data2.Should().Contain("Broadcast");
        blank2.Should().BeEmpty();
    }
}
