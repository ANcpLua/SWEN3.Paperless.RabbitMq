using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using SWEN3.Paperless.RabbitMq.GenAI;

namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GeminiServiceTests
{
    private readonly Mock<ILogger<GeminiService>> _loggerMock = new();

    private readonly GeminiOptions _options = new()
    {
        ApiKey = "test-key",
        Model = "gemini-2.0-flash",
        TimeoutSeconds = 5
    };

    [Fact]
    public async Task SummarizeAsync_WithValidText_ReturnsSummary()
    {
        const string inputText = "This is a test document about quarterly earnings.";
        const string expectedSummary = "Executive summary: Financial performance improved.";

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, CreateGeminiResponse(expectedSummary));
        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync(inputText, TestContext.Current.CancellationToken);

        result.Should().Be(expectedSummary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SummarizeAsync_WithInvalidText_ReturnsNull(string? invalidText)
    {
        var service = CreateService(new HttpClient());

        var result = await service.SummarizeAsync(invalidText!, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("{}", "No candidates")]
    [InlineData("{\"candidates\":[]}", "Empty candidates")]
    [InlineData("{\"candidates\":[{}]}", "No content")]
    [InlineData("{\"candidates\":[{\"content\":{}}]}", "No parts")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[]}}]}", "Empty parts")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{}]}}]}", "No text")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"   \"}]}}]}", "Whitespace only")]
    public async Task SummarizeAsync_WithMalformedResponse_ReturnsNull(string json, string scenario)
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync("test text", TestContext.Current.CancellationToken);

        result.Should().BeNull($"JSON scenario: {scenario}");
    }

    [Fact]
    public async Task SummarizeAsync_WithApiError_ReturnsNull()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "Error");
        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync("test text", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SummarizeAsync_WithSuccessfulResponse_ReturnsSummary()
    {
        const string expectedSummary = "Test summary from API";
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, CreateGeminiResponse(expectedSummary));
        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync("test text", TestContext.Current.CancellationToken);

        result.Should().Be(expectedSummary);
    }

    [Fact]
    public async Task SummarizeAsync_WithMalformedJson_ReturnsNull()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, "invalid json");
        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync("test text", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SummarizeAsync_WhenCanceled_ReturnsNull()
    {
        var httpClient = new HttpClient(new CanceledMessageHandler());
        var options = Options.Create(new GeminiOptions { ApiKey = "test", Model = "test" });
        var logger = new Mock<ILogger<GeminiService>>();
        var service = new GeminiService(httpClient, options, logger.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await service.SummarizeAsync("test text", cts.Token);

        result.Should().BeNull();
    }

    private GeminiService CreateService(HttpClient httpClient) =>
        new(httpClient, Options.Create(_options), _loggerMock.Object);

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content)
                });

        return new HttpClient(handlerMock.Object);
    }

    private static string CreateGeminiResponse(string text)
    {
        return JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text } } } } }
        });
    }

    private class CanceledMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("Canceled", innerException: null, cancellationToken);
    }
}
