namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

[SuppressMessage("Design", "MA0051:Method is too long")]
public class SseExtensionsNet10Tests
{
#if NET10_0_OR_GREATER
    [Fact]
    public async Task MapSse_Net10_ShouldStreamEventsUsingNativeApi()
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

        // Publish repeatedly until subscriber connects
        var publisherTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(50, cts.Token);
                    sseStream.Publish(new Messages.SseTestEvent { Id = 1, Message = "Hello" });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }, CancellationToken.None);

        try
        {
            using var response = await responseTask;
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var line1 = await reader.ReadLineAsync(cts.Token);
            var line2 = await reader.ReadLineAsync(cts.Token);
            var line3 = await reader.ReadLineAsync(cts.Token);

            line1.Should().Be("event: test-event");
            line2.Should().Be("data: {\"id\":1,\"msg\":\"Hello\"}");
            line3.Should().BeEmpty();
        }
        finally
        {
            await cts.CancelAsync();
            await publisherTask;
        }
    }

    [Fact]
    public async Task MapSse_Net10_ShouldStreamMultipleEvents()
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

        var events = new[]
        {
            new Messages.SseTestEvent { Id = 1, Message = "First" },
            new Messages.SseTestEvent { Id = 2, Message = "Second" },
            new Messages.SseTestEvent { Id = 3, Message = "Third" }
        };

        // Publish repeatedly
        var publisherTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(50, cts.Token);
                    foreach (var evt in events)
                    {
                        sseStream.Publish(evt);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }, CancellationToken.None);

        try
        {
            using var response = await responseTask;
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // Read first event
            var event1Line1 = await reader.ReadLineAsync(cts.Token);
            var data1Line1 = await reader.ReadLineAsync(cts.Token);
            var blank1 = await reader.ReadLineAsync(cts.Token);

            // Read second event
            var event2Line1 = await reader.ReadLineAsync(cts.Token);
            var data2Line1 = await reader.ReadLineAsync(cts.Token);
            var blank2 = await reader.ReadLineAsync(cts.Token);

            // Read third event
            var event3Line1 = await reader.ReadLineAsync(cts.Token);
            var data3Line1 = await reader.ReadLineAsync(cts.Token);
            var blank3 = await reader.ReadLineAsync(cts.Token);

            // Assert
            event1Line1.Should().Be("event: test-event");
            data1Line1.Should().Be("data: {\"id\":1,\"msg\":\"First\"}");
            blank1.Should().BeEmpty();

            event2Line1.Should().Be("event: test-event");
            data2Line1.Should().Be("data: {\"id\":2,\"msg\":\"Second\"}");
            blank2.Should().BeEmpty();

            event3Line1.Should().Be("event: test-event");
            data3Line1.Should().Be("data: {\"id\":3,\"msg\":\"Third\"}");
            blank3.Should().BeEmpty();
        }
        finally
        {
            await cts.CancelAsync();
            await publisherTask;
        }
    }

    [Fact]
    public async Task MapSse_Net10_CompletesIteratorWhenClientDisconnects()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: null,
            configureEndpoints: e => e.MapSse<Messages.SseTestEvent>("/sse",
                m => new { m.Id, m.Message },
                _ => "evt"));

        using var _ = host;
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
        var sseStream = host.Services.GetRequiredService<ISseStream<Messages.SseTestEvent>>();

        var responseTask = client.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Publish one event after connection - allows iterator to complete when client disconnects
        var publisherTask = Task.Run(async () =>
        {
            await Task.Delay(100, cts.Token);
            sseStream.Publish(new Messages.SseTestEvent { Id = 1, Message = "Hi" });
        }, CancellationToken.None);

        try
        {
            var resp = await responseTask;
            resp.EnsureSuccessStatusCode();

            await using var body = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(body);

            // Read the one event
            var eventLine = await reader.ReadLineAsync(cts.Token);
            var dataLine = await reader.ReadLineAsync(cts.Token);
            var blankLine = await reader.ReadLineAsync(cts.Token);

            eventLine.Should().Be("event: evt");
            dataLine.Should().Be("data: {\"id\":1,\"message\":\"Hi\"}");
            blankLine.Should().BeEmpty();

            // Cancel to trigger RequestAborted, which completes the iterator naturally
            await cts.CancelAsync();
        }
        finally
        {
            await publisherTask;
        }
    }

    [Fact]
    public async Task MapSse_Net10_CompletesNaturallyWhenStreamEnds()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var fakeStream = new Helpers.FakeCompletableSseStream<Messages.SseTestEvent>();

        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<Messages.SseTestEvent>(
            configureServices: services => services.AddSingleton<ISseStream<Messages.SseTestEvent>>(fakeStream),
            configureEndpoints: e => e.MapSse<Messages.SseTestEvent>("/sse",
                m => new { m.Id, m.Message },
                _ => "final-event"));

        using var _ = host;
        var client = server.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        // Act - Connect without cancellation token to avoid cancellation-based termination
        var responseTask = client.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

        // Wait for connection to establish
        await Task.Delay(100, cts.Token);

        // Publish one event
        fakeStream.Publish(new Messages.SseTestEvent { Id = 99, Message = "Done" });

        var response = await responseTask.WaitAsync(cts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Read the event
        var eventLine = await reader.ReadLineAsync(cts.Token);
        var dataLine = await reader.ReadLineAsync(cts.Token);
        var blankLine = await reader.ReadLineAsync(cts.Token);

        eventLine.Should().Be("event: final-event");
        dataLine.Should().Be("data: {\"id\":99,\"message\":\"Done\"}");
        blankLine.Should().BeEmpty();

        // Complete the stream to end ReadAllAsync naturally
        fakeStream.Complete();

        // Verify the stream ends (ReadLineAsync returns null)
        var endLine = await reader.ReadLineAsync(cts.Token);
        endLine.Should().BeNull("stream should end naturally after Complete()");

        // Note: In NET10, RequestAborted handler only fires on actual cancellation,
        // not on natural stream completion, so ClientCount stays 1 (expected behavior)
    }
#endif
}
