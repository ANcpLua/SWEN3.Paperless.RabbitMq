namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class OcrEventStreamIntegrationTests
{
    [Theory]
    [InlineData("Completed", "ocr-completed")]
    [InlineData("Failed", "ocr-failed")]
    [InlineData("Processing", "ocr-failed")]
    public async Task MapOcrEventStream_ShouldEmitCorrectEventType(string status, string expectedEventType)
    {
        var sseStream = new SseStream<OcrEvent>();
        var ocrEvent = new OcrEvent(Guid.NewGuid(), status, status is "Completed" ? "Text" : null,
            DateTimeOffset.UtcNow);

        using var server = TestServerFactory.CreateSseTestServer(sseStream,
            endpoints => endpoints.MapOcrEventStream());
        using var client = server.CreateClient();

        var responseTask = client.GetAsync("/api/v1/ocr-results", HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        sseStream.Publish(ocrEvent);

        var response = await responseTask;
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var eventLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        eventLine.Should().Be($"event: {expectedEventType}");

        response.Dispose();
    }
}
