using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq.Protected;
using SWEN3.Paperless.RabbitMq.GenAI;

namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GeminiServiceTests
{
    private readonly Mock<ILogger<GeminiService>> _loggerMock = new();

    private readonly GeminiOptions _options = new()
        { ApiKey = "test-key", Model = "gemini-2.0-flash", MaxRetries = 2, TimeoutSeconds = 5 };

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

        _loggerMock.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Empty text")), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
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
    public async Task SummarizeAsync_WithApiError_RetriesAndReturnsNull()
    {
        var callCount = 0;
        var httpClient = CreateMockHttpClient(() =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync("test text", TestContext.Current.CancellationToken);

        result.Should().BeNull();
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task SummarizeAsync_WithRateLimitThenSuccess_RetriesAndSucceeds()
    {
        var callCount = 0;
        var httpClient = CreateMockHttpClient(() =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(CreateGeminiResponse("Retry succeeded")) };
        });

        var service = CreateService(httpClient);

        var result = await service.SummarizeAsync("test text", TestContext.Current.CancellationToken);

        result.Should().Be("Retry succeeded");
        callCount.Should().Be(2);
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
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test",
            Model = "test",
            MaxRetries = 1
        });
        var logger = new Mock<ILogger<GeminiService>>();
        var service = new GeminiService(httpClient, options, logger.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await service.SummarizeAsync("test text", cts.Token);

        result.Should().BeNull();
        logger.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("failed after")),
                It.IsAny<TaskCanceledException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private GeminiService CreateService(HttpClient httpClient)
    {
        return new GeminiService(httpClient, Options.Create(_options), _loggerMock.Object);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage(statusCode)
                { Content = new StringContent(content) });

        return new HttpClient(handlerMock.Object);
    }

    private static HttpClient CreateMockHttpClient(Func<HttpResponseMessage> responseFactory)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(responseFactory);

        return new HttpClient(handlerMock.Object);
    }

    private static string CreateGeminiResponse(string text)
    {
        return JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text }
                        }
                    }
                }
            }
        });
    }

    private class CanceledMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("Canceled", null, cancellationToken);
        }
    }
}