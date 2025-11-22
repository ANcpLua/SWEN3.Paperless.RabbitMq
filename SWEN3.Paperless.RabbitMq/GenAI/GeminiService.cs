using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Google Gemini AI implementation of <see cref="ITextSummarizer" /> for document summarization.
///     <para>Resilience (retries, circuit breaker, timeouts) is handled by Microsoft.Extensions.Http.Resilience.</para>
/// </summary>
public sealed partial class GeminiService : ITextSummarizer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiService> _logger;
    private readonly GeminiOptions _options;

    /// <summary>Initializes a new instance of <see cref="GeminiService" />.</summary>
    /// <param name="httpClient">HTTP client configured with resilience handlers.</param>
    /// <param name="options">Gemini API options (API key, model, timeout).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public GeminiService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>Generates a structured summary for the provided text using Gemini.</summary>
    /// <param name="text">OCR-extracted text to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated summary, or null on validation/API failure.</returns>
    public async Task<string?> SummarizeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            GeminiServiceLog.EmptyText(_logger);
            return null;
        }

        var prompt = BuildPrompt(text);
        var body = BuildRequestBody(prompt);
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                GeminiServiceLog.GeminiApiError(_logger, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ExtractSummary(responseContent);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            GeminiServiceLog.GeminiApiCallFailed(_logger, ex);
            return null;
        }
    }

    private static string BuildPrompt(string text) =>
        $"""
         You are a document summarization assistant for a Document Management System (DMS).
         Your task is to analyse the following OCR-extracted text and provide a structured summary.

         Instructions:
         1. Create a concise executive summary (2-3 sentences)
         2. List 3-5 key points from the document
         3. Identify the document type if possible
         4. Extract any important dates, numbers or entities mentioned
         5. Keep the summary factual and objective - do not add interpretations

         Document text:
         ---
         {text}
         ---

         Provide the summary now.
         """;

    private static object BuildRequestBody(string prompt) =>
        new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.3, topK = 40, topP = 0.95, maxOutputTokens = 1024 }
        };

    private string? ExtractSummary(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates))
            {
                GeminiServiceLog.NoCandidates(_logger);
                return null;
            }

            if (candidates.GetArrayLength() is 0)
            {
                GeminiServiceLog.EmptyCandidates(_logger);
                return null;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content))
            {
                GeminiServiceLog.NoContent(_logger);
                return null;
            }

            if (!content.TryGetProperty("parts", out var parts))
            {
                GeminiServiceLog.NoParts(_logger);
                return null;
            }

            if (parts.GetArrayLength() is 0)
            {
                GeminiServiceLog.EmptyParts(_logger);
                return null;
            }

            if (!parts[0].TryGetProperty("text", out var textElement))
            {
                GeminiServiceLog.NoText(_logger);
                return null;
            }

            var extractedText = textElement.GetString();
            return string.IsNullOrWhiteSpace(extractedText) ? null : extractedText;
        }
        catch (Exception ex)
        {
            GeminiServiceLog.ParseError(_logger, ex);
            return null;
        }
    }

    internal static partial class GeminiServiceLog
    {
        [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Empty text supplied to summarizer")]
        public static partial void EmptyText(ILogger logger);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Gemini API responded {StatusCode}: {Reason}")]
        public static partial void GeminiApiError(ILogger logger, HttpStatusCode statusCode, string? reason);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Gemini API call failed")]
        public static partial void GeminiApiCallFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "No candidates in Gemini response")]
        public static partial void NoCandidates(ILogger logger);

        [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "Empty candidates array in Gemini response")]
        public static partial void EmptyCandidates(ILogger logger);

        [LoggerMessage(EventId = 2006, Level = LogLevel.Warning, Message = "No content in first candidate")]
        public static partial void NoContent(ILogger logger);

        [LoggerMessage(EventId = 2007, Level = LogLevel.Warning, Message = "No parts in content")]
        public static partial void NoParts(ILogger logger);

        [LoggerMessage(EventId = 2008, Level = LogLevel.Warning, Message = "Empty parts array in content")]
        public static partial void EmptyParts(ILogger logger);

        [LoggerMessage(EventId = 2009, Level = LogLevel.Warning, Message = "No text in first part")]
        public static partial void NoText(ILogger logger);

        [LoggerMessage(EventId = 2010, Level = LogLevel.Error, Message = "Failed to parse Gemini response")]
        public static partial void ParseError(ILogger logger, Exception exception);
    }
}
