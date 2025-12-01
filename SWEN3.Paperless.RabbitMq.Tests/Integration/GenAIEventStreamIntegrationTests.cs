namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class GenAIEventStreamIntegrationTests
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("Completed", "genai-completed")]
    [InlineData("Failed", "genai-failed")]
    public async Task MapGenAIEventStream_ShouldEmitCorrectEventType(string status, string expectedEventType)
    {
        var fakeStream = new FakeCompletableSseStream<GenAIEvent>();
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<GenAIEvent>(
            configureServices: s => s.AddSingleton<ISseStream<GenAIEvent>>(fakeStream),
            configureEndpoints: endpoints => endpoints.MapGenAIEventStream());

        using var _ = host;
        var client = server.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var readTask = Task.Run(async () =>
        {
            using var response =
                await client.GetAsync("/api/v1/events/genai", HttpCompletionOption.ResponseHeadersRead, ct);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (true)
            {
                var line = await reader.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
                if (line == null)
                    return null;
                if (line.StartsWith("event:"))
                    return line;
            }
        }, ct);

        await SseTestHelpers.WaitForClientsAsync(fakeStream, stabilize: false, cancellationToken: ct);

        var genAiEvent = status == "Completed"
            ? new GenAIEvent(Guid.NewGuid(), "Test summary", DateTimeOffset.UtcNow)
            : new GenAIEvent(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow, "Service error");
        fakeStream.Publish(genAiEvent);
        fakeStream.Complete();

        var eventLine = await readTask;

        eventLine.Should().Be($"event: {expectedEventType}");
    }

    [Fact]
    public async Task MapGenAIEventStream_WithMultipleEvents_ShouldStreamInOrder()
    {
        var fakeStream = new FakeCompletableSseStream<GenAIEvent>();
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<GenAIEvent>(
            configureServices: s => s.AddSingleton<ISseStream<GenAIEvent>>(fakeStream),
            configureEndpoints: endpoints => endpoints.MapGenAIEventStream());

        using var _ = host;
        var client = server.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var readTask = Task.Run(() => ReadEventsAsync(client, 3, ct), ct);

        await SseTestHelpers.WaitForClientsAsync(fakeStream, cancellationToken: ct);

        fakeStream.Publish(new GenAIEvent(Guid.NewGuid(), "Summary 1", DateTimeOffset.UtcNow));
        await Task.Delay(50, ct);

        fakeStream.Publish(new GenAIEvent(Guid.NewGuid(), "Summary 2", DateTimeOffset.UtcNow));
        await Task.Delay(50, ct);

        fakeStream.Publish(new GenAIEvent(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow, "Error occurred"));
        fakeStream.Complete();

        var events = await readTask;

        events[0].Event.Should().Be("event: genai-completed");
        events[0].Data.Should().Contain("Summary 1");

        events[1].Event.Should().Be("event: genai-completed");
        events[1].Data.Should().Contain("Summary 2");

        events[2].Event.Should().Be("event: genai-failed");
        events[2].Data.Should().Contain("Error occurred");
    }

    private static async Task<List<(string Event, string Data)>> ReadEventsAsync(HttpClient client, int count,
        CancellationToken cancellationToken = default)
    {
        using (client)
        {
            using var response =
                await client.GetAsync("/api/v1/events/genai", HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var events = new List<(string Event, string Data)>();
            string? currentEvent = null;
            string? currentData = null;

            while (events.Count < count)
            {
                var line = await reader.ReadLineAsync(cancellationToken).AsTask().WaitAsync(ReadTimeout, cancellationToken);
                if (line == null)
                    break;

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentEvent != null && currentData != null)
                    {
                        events.Add((currentEvent, currentData));
                        currentEvent = null;
                        currentData = null;
                    }

                    continue;
                }

                if (line.StartsWith("event:"))
                    currentEvent = line;
                else if (line.StartsWith("data:"))
                    currentData = line;
            }

            return events;
        }
    }
}
