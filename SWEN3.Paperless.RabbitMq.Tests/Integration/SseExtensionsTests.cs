using System.Text.Json.Nodes;

namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class SseExtensionsTests
{
    private const string TestEndpoint = "/sse-test";
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task MapSse_WithPublishedEvent_ShouldStreamToClient()
    {
        var fakeStream = new FakeCompletableSseStream<Messages.SseTestEvent>();
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: s => s.AddSingleton<ISseStream<Messages.SseTestEvent>>(fakeStream),
            configureEndpoints: endpoints => endpoints.MapSse<Messages.SseTestEvent>(
                TestEndpoint, evt => new { evt.Id, evt.Message }, _ => "test-event"));

        using var _ = host;
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        var ct = TestContext.Current.CancellationToken;

        var responseTask = client.GetAsync(TestEndpoint, HttpCompletionOption.ResponseHeadersRead, ct);

        await SseTestHelpers.WaitForClientsAsync(fakeStream, cancellationToken: ct);

        var testEvent = new Messages.SseTestEvent { Id = 42, Message = "Hello SSE" };
        fakeStream.Publish(testEvent);
        fakeStream.Complete();

        using var response = await responseTask;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var eventLine = await reader.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
        var dataLine = await reader.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
        var blankLine = await reader.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);

        eventLine.Should().Be("event: test-event");
        dataLine.Should().StartWith("data: ");

        var json = JsonNode.Parse(dataLine!["data: ".Length..])!;
        (json["Id"] ?? json["id"])!.GetValue<int>().Should().Be(42);
        (json["Message"] ?? json["message"])!.GetValue<string>().Should().Be("Hello SSE");

        blankLine.Should().BeEmpty();
    }

    [Fact]
    [SuppressMessage("Design", "MA0051:Method is too long")]
    public async Task MapSse_MultipleClients_ShouldReceiveSameEvent()
    {
        var fakeStream = new FakeCompletableSseStream<Messages.SseTestEvent>();
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: s => s.AddSingleton<ISseStream<Messages.SseTestEvent>>(fakeStream),
            configureEndpoints: endpoints => endpoints.MapSse<Messages.SseTestEvent>(
                TestEndpoint, evt => new { evt.Id, evt.Message }, _ => "test-event"));

        using var _ = host;
        var ct = TestContext.Current.CancellationToken;

        var client1 = server.CreateClient();
        var client2 = server.CreateClient();
        client1.Timeout = Timeout.InfiniteTimeSpan;
        client2.Timeout = Timeout.InfiniteTimeSpan;

        var responseTask1 = client1.GetAsync(TestEndpoint, HttpCompletionOption.ResponseHeadersRead, ct);
        var responseTask2 = client2.GetAsync(TestEndpoint, HttpCompletionOption.ResponseHeadersRead, ct);

        await SseTestHelpers.WaitForClientsAsync(fakeStream, expectedClients: 2, cancellationToken: ct);

        var testEvent = new Messages.SseTestEvent { Id = 99, Message = "Broadcast" };
        fakeStream.Publish(testEvent);
        fakeStream.Complete();

        using var response1 = await responseTask1;
        using var response2 = await responseTask2;

        response1.EnsureSuccessStatusCode();
        response2.EnsureSuccessStatusCode();

        await using var stream1 = await response1.Content.ReadAsStreamAsync(ct);
        await using var stream2 = await response2.Content.ReadAsStreamAsync(ct);
        using var reader1 = new StreamReader(stream1, Encoding.UTF8);
        using var reader2 = new StreamReader(stream2, Encoding.UTF8);

        var event1 = await reader1.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
        var data1 = await reader1.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
        var blank1 = await reader1.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);

        var event2 = await reader2.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
        var data2 = await reader2.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
        var blank2 = await reader2.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);

        event1.Should().Be("event: test-event");
        data1.Should().StartWith("data: ");
        var json1 = JsonNode.Parse(data1!["data: ".Length..])!;
        (json1["Id"] ?? json1["id"])!.GetValue<int>().Should().Be(99);
        (json1["Message"] ?? json1["message"])!.GetValue<string>().Should().Be("Broadcast");
        blank1.Should().BeEmpty();

        event2.Should().Be("event: test-event");
        data2.Should().StartWith("data: ");
        var json2 = JsonNode.Parse(data2!["data: ".Length..])!;
        (json2["Id"] ?? json2["id"])!.GetValue<int>().Should().Be(99);
        (json2["Message"] ?? json2["message"])!.GetValue<string>().Should().Be("Broadcast");
        blank2.Should().BeEmpty();
    }
}
