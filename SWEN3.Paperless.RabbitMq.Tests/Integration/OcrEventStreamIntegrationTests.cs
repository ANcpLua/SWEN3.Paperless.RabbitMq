namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public sealed class OcrEventStreamIntegrationTests
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("Completed", "ocr-completed")]
    [InlineData("Failed", "ocr-failed")]
    [InlineData("Processing", "ocr-failed")]
    public async Task MapOcrEventStream_ShouldEmitCorrectEventType(string status, string expectedEventType)
    {
        var fakeStream = new FakeCompletableSseStream<OcrEvent>();
        var (host, server) = await SseTestHelpers.CreateSseTestServerAsync<OcrEvent>(
            configureServices: s => s.AddSingleton<ISseStream<OcrEvent>>(fakeStream),
            configureEndpoints: endpoints => endpoints.MapOcrEventStream());

        using var _ = host;
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
                var line = await reader.ReadLineAsync(ct).AsTask().WaitAsync(ReadTimeout, ct);
                if (line == null)
                    return null;
                if (line.StartsWith("event:"))
                    return line;
            }
        }, ct);

        await SseTestHelpers.WaitForClientsAsync(fakeStream, cancellationToken: ct);

        var ocrEvent = new OcrEvent(Guid.NewGuid(), status, status is "Completed" ? "Text" : null,
            DateTimeOffset.UtcNow);
        fakeStream.Publish(ocrEvent);
        fakeStream.Complete();

        var eventLine = await readTask;

        eventLine.Should().Be($"event: {expectedEventType}");
    }
}
