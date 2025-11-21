namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public sealed class OcrEventStreamIntegrationTests
{
    [Theory]
    [InlineData("Completed", "ocr-completed")]
    [InlineData("Failed", "ocr-failed")]
    [InlineData("Processing", "ocr-failed")]
    public async Task MapOcrEventStream_ShouldEmitCorrectEventType(string status, string expectedEventType)
    {
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<OcrEvent>(
            configureServices: null,
            configureEndpoints: endpoints => endpoints.MapOcrEventStream());

        using var _ = host;
        var sseStream = host.Services.GetRequiredService<ISseStream<OcrEvent>>();
        var ct = TestContext.Current.CancellationToken;

        var readTask = Task.Run(async () =>
        {
            using var client = server.CreateClient();
            using var response = await client.GetAsync("/api/v1/ocr-results", HttpCompletionOption.ResponseHeadersRead,
                ct);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (true)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null)
                    return null;
                if (line.StartsWith("event:"))
                    return line;
            }
        }, ct);

        // Wait for client to connect with timeout
        var waitStart = DateTime.UtcNow;
        while (sseStream.ClientCount == 0)
        {
            if (DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(10))
                throw new TimeoutException("Client did not connect within timeout");
            await Task.Delay(50, ct);
        }

        // Give the HTTP connection a moment to stabilize
        await Task.Delay(100, ct);

        var ocrEvent = new OcrEvent(Guid.NewGuid(), status, status is "Completed" ? "Text" : null,
            DateTimeOffset.UtcNow);
        sseStream.Publish(ocrEvent);

        var eventLine = await readTask;

        eventLine.Should().Be($"event: {expectedEventType}");
    }
}
