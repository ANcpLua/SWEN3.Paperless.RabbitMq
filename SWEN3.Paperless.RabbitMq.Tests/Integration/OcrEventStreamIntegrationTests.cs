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

        var readTask = Task.Run(async () =>
        {
            using var client = server.CreateClient();
            var response = await client.GetAsync("/api/v1/ocr-results", HttpCompletionOption.ResponseHeadersRead);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var line = await reader.ReadLineAsync();
            response.Dispose();
            return line;
        });

        await Task.Delay(300, TestContext.Current.CancellationToken);

        var ocrEvent = new OcrEvent(Guid.NewGuid(), status, status is "Completed" ? "Text" : null,
            DateTimeOffset.UtcNow);
        sseStream.Publish(ocrEvent);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var eventLine = await readTask.WaitAsync(cts.Token);

        eventLine.Should().Be($"event: {expectedEventType}");
    }
}