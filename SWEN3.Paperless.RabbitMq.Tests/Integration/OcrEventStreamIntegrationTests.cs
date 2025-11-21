namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public sealed class OcrEventStreamIntegrationTests
{
    [Theory]
    [InlineData("Completed", "ocr-completed")]
    [InlineData("Failed", "ocr-failed")]
    [InlineData("Processing", "ocr-failed")]
    public async Task MapOcrEventStream_ShouldEmitCorrectEventType(string status, string expectedEventType)
    {
        var sseStream = new SseStream<OcrEvent>();
        var server = SseTestHelpers.CreateSseTestServer(sseStream, endpoints => endpoints.MapOcrEventStream());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var readTask = Task.Run(async () =>
        {
            using var client = server.CreateClient();
            using var response = await client.GetAsync("/api/v1/ocr-results", HttpCompletionOption.ResponseHeadersRead,
                cts.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            while (true)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line == null)
                    return null;
                if (line.StartsWith("event:"))
                    return line;
            }
        }, cts.Token);

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

        var ocrEvent = new OcrEvent(Guid.NewGuid(), status, status is "Completed" ? "Text" : null,
            DateTimeOffset.UtcNow);
        sseStream.Publish(ocrEvent);

        var eventLine = await readTask.WaitAsync(cts.Token);

        eventLine.Should().Be($"event: {expectedEventType}");
    }
}
