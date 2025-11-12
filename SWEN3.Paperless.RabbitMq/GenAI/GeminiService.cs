using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Google Gemini AI implementation of <see cref="ITextSummarizer" /> for document summarization.
///     <para>Provides structured text summarization with automatic retry logic, timeout handling, and robust error recovery.</para>
///     <para>Integrates with Google's Generative Language API using the Gemini 2.0 Flash model by default.</para>
/// </summary>
/// <remarks>
///     Resilience (retries, circuit breaker, timeouts) is handled automatically by
///     Microsoft.Extensions.Http.Resilience via AddStandardResilienceHandler.
/// </remarks>
public sealed class GeminiService : ITextSummarizer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiService> _logger;
    private readonly GeminiOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GeminiService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making API requests (configured with resilience handlers).</param>
    /// <param name="options">Configuration options containing API key, model selection, and timeout settings.</param>
    /// <param name="logger">Logger instance for diagnostic output and error tracking.</param>
    public GeminiService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    ///     Generates a structured summary of the provided text using Google's Gemini AI model.
    ///     Includes executive summary, key points, document type identification, and entity extraction.
    /// </summary>
    /// <param name="text">The OCR-extracted text to summarize. Must not be null or whitespace.</param>
    /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
    /// <returns>
    ///     A structured summary containing key insights from the document, or <c>null</c> if the API call fails,
    ///     times out, or the input text is invalid.
    /// </returns>
    public async Task<string?> SummarizeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Empty text supplied to summarizer");
            return null;
        }

        var prompt = BuildPrompt(text);
        var body = BuildRequestBody(prompt);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API responded {StatusCode}: {Reason}", response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ExtractSummary(responseContent);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogError(ex, "Gemini API call failed");
            return null;
        }
    }

    private static string BuildPrompt(string text)
    {
        return $"""
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
    }

    private static object BuildRequestBody(string prompt)
    {
        return new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 1024
            }
        };
    }

    private string? ExtractSummary(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates))
            {
                _logger.LogWarning("No candidates in Gemini response");
                return null;
            }

            if (candidates.GetArrayLength() is 0)
            {
                _logger.LogWarning("Empty candidates array in Gemini response");
                return null;
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content))
            {
                _logger.LogWarning("No content in first candidate");
                return null;
            }

            if (!content.TryGetProperty("parts", out var parts))
            {
                _logger.LogWarning("No parts in content");
                return null;
            }

            if (parts.GetArrayLength() is 0)
            {
                _logger.LogWarning("Empty parts array in content");
                return null;
            }

            if (!parts[0].TryGetProperty("text", out var textElement))
            {
                _logger.LogWarning("No text in first part");
                return null;
            }

            var extractedText = textElement.GetString();
            return string.IsNullOrWhiteSpace(extractedText) ? null : extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini response");
            return null;
        }
    }
}