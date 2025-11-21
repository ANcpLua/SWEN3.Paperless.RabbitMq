namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class GenAIEventStreamIntegrationTests
{
    [Theory]
    [InlineData("Completed", "genai-completed")]
    [InlineData("Failed", "genai-failed")]
    public async Task MapGenAIEventStream_ShouldEmitCorrectEventType(string status, string expectedEventType)
    {
        var sseStream = new SseStream<GenAIEvent>();
        using var server = SseTestHelpers.CreateSseTestServer(sseStream, endpoints => endpoints.MapGenAIEventStream());
        var client = server.CreateClient();

        var readTask = Task.Run(async () =>
        {
            using var response =
                await client.GetAsync("/api/v1/events/genai", HttpCompletionOption.ResponseHeadersRead);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    return null;
                if (line.StartsWith("event:"))
                    return line;
            }
        });

        while (sseStream.ClientCount == 0)
            await Task.Delay(50, TestContext.Current.CancellationToken);

        var genAiEvent = status == "Completed"
            ? new GenAIEvent(Guid.NewGuid(), "Test summary", DateTimeOffset.UtcNow)
            : new GenAIEvent(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow, "Service error");
        sseStream.Publish(genAiEvent);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var eventLine = await readTask.WaitAsync(cts.Token);

        eventLine.Should().Be($"event: {expectedEventType}");
    }

    [Fact]
    public async Task MapGenAIEventStream_WithMultipleEvents_ShouldStreamInOrder()
    {
        var sseStream = new SseStream<GenAIEvent>();
        using var server = SseTestHelpers.CreateSseTestServer(sseStream, endpoints => endpoints.MapGenAIEventStream());

        var client = server.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = cts.Token;
        var readTask = Task.Run(() => ReadEventsAsync(client, 3, token), token);

        // Wait for client to connect with timeout
        var waitStart = DateTime.UtcNow;
        while (sseStream.ClientCount == 0)
        {
            if (DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(10))
                throw new TimeoutException("Client did not connect within timeout");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        // Give the HTTP connection a moment to stabilize
        await Task.Delay(100, TestContext.Current.CancellationToken);

        sseStream.Publish(new GenAIEvent(Guid.NewGuid(), "Summary 1", DateTimeOffset.UtcNow));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        sseStream.Publish(new GenAIEvent(Guid.NewGuid(), "Summary 2", DateTimeOffset.UtcNow));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        sseStream.Publish(new GenAIEvent(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow, "Error occurred"));

        var events = await readTask.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

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
                var line = await reader.ReadLineAsync(cancellationToken);
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
