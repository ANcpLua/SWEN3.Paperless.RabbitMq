namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

[SuppressMessage("Design", "MA0051:Method is too long")]
public static class SseExtensionsFallbackTests
{
#if !NET10_0_OR_GREATER
    [Fact]
    public static async Task MapSse_Fallback_ShouldWriteCorrectSseFormat()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: null,
            configureEndpoints: e => e.MapSse<Messages.SseTestEvent>("/sse",
                evt => new { id = evt.Id, msg = evt.Message },
                _ => "test-event"));

        using var _ = host;
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        var sseStream = host.Services.GetRequiredService<ISseStream<Messages.SseTestEvent>>();

        // Act
        var responseTask = client.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Wait for client to connect
        while (sseStream.ClientCount == 0)
            await Task.Delay(50, cts.Token);

        // Publish once
        sseStream.Publish(new Messages.SseTestEvent { Id = 1, Message = "Hello" });

        using var response = await responseTask;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var line1 = await reader.ReadLineAsync(cts.Token);
        var line2 = await reader.ReadLineAsync(cts.Token);
        var line3 = await reader.ReadLineAsync(cts.Token);

        line1.Should().Be("event: test-event");
        line2.Should().Be("data: {\"id\":1,\"msg\":\"Hello\"}");
        line3.Should().BeEmpty(); // The double newline
    }

    [Fact]
    [SuppressMessage("Design", "MA0051:Method is too long")]
    public static async Task MapSse_Fallback_ValidatesHeadersAndMultiEventPayload()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: null,
            configureEndpoints: e => e.MapSse<Messages.SseTestEvent>("/sse",
                evt => new { id = evt.Id, msg = evt.Message },
                _ => "multi-event"));

        using var _ = host;
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        var sseStream = host.Services.GetRequiredService<ISseStream<Messages.SseTestEvent>>();

        // Act
        var responseTask = client.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        var events = new[]
        {
            new Messages.SseTestEvent { Id = 1, Message = "First" },
            new Messages.SseTestEvent { Id = 2, Message = "Second" }
        };

        // Wait for client to connect
        while (sseStream.ClientCount == 0)
            await Task.Delay(50, cts.Token);

        // Publish events once
        foreach (var evt in events)
        {
            sseStream.Publish(evt);
        }

        using var response = await responseTask;

        // Assert headers
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl?.NoCache.Should().BeTrue();
        response.Headers.Connection.Should().Contain("keep-alive");
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Read first event
        var event1 = await reader.ReadLineAsync(cts.Token);
        var data1 = await reader.ReadLineAsync(cts.Token);
        var blank1 = await reader.ReadLineAsync(cts.Token);

        event1.Should().Be("event: multi-event");
        data1.Should().Be("data: {\"id\":1,\"msg\":\"First\"}");
        blank1.Should().BeEmpty();

        // Read second event
        var event2 = await reader.ReadLineAsync(cts.Token);
        var data2 = await reader.ReadLineAsync(cts.Token);
        var blank2 = await reader.ReadLineAsync(cts.Token);

        event2.Should().Be("event: multi-event");
        data2.Should().Be("data: {\"id\":2,\"msg\":\"Second\"}");
        blank2.Should().BeEmpty();
    }

    [Fact]
    public static async Task MapSse_Fallback_CompletesNaturallyWhenStreamEnds()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var fakeStream = new FakeCompletableSseStream<Messages.SseTestEvent>();

        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: services => services.AddSingleton<ISseStream<Messages.SseTestEvent>>(fakeStream),
            configureEndpoints: e => e.MapSse<Messages.SseTestEvent>("/sse",
                evt => new { id = evt.Id, msg = evt.Message },
                _ => "complete-event"));

        using var _ = host;
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        // Act - connect with test token
        var responseTask = client.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Wait for connection to establish (subscriber registered)
        var start = DateTime.UtcNow;
        while (fakeStream.ClientCount == 0)
        {
            if (DateTime.UtcNow - start > TimeSpan.FromSeconds(5))
                throw new TimeoutException("SSE client did not connect");
            await Task.Delay(50, cts.Token);
        }

        // Publish one event
        fakeStream.Publish(new Messages.SseTestEvent { Id = 42, Message = "Final" });

        var response = await responseTask.WaitAsync(cts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Read the event
        var eventLine = await reader.ReadLineAsync(cts.Token);
        var dataLine = await reader.ReadLineAsync(cts.Token);
        var blankLine = await reader.ReadLineAsync(cts.Token);

        eventLine.Should().Be("event: complete-event");
        dataLine.Should().Be("data: {\"id\":42,\"msg\":\"Final\"}");
        blankLine.Should().BeEmpty();

        // Complete the stream to end ReadAllAsync naturally
        fakeStream.Complete();

        // Verify the stream ends (ReadLineAsync returns null)
        var endLine = await reader.ReadLineAsync(cts.Token);
        endLine.Should().BeNull("stream should end naturally after Complete()");

        // Verify Unsubscribe was called in finally block
        await Task.Delay(50, cts.Token); // Allow finally block to execute
        fakeStream.ClientCount.Should().Be(0, "finally block should have called Unsubscribe");
    }
#endif
}
