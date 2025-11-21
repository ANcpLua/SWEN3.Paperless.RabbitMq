using System.ComponentModel.DataAnnotations;

namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Configuration options for the Google Gemini API integration.
/// </summary>
/// <remarks>
///     These options control the behavior of the <see cref="GeminiService" /> when making API calls to Google's Gemini
///     model.
///     Configure these options through appsettings.json or environment variables.
/// </remarks>
public sealed class GeminiOptions
{
    /// <summary>
    ///     Configuration section name for Gemini options.
    /// </summary>
    public const string SectionName = "Gemini";

    /// <summary>
    ///     Gets or initializes the API key for authenticating with the Gemini API.
    /// </summary>
    /// <value>
    ///     The API key obtained from Google AI Studio. Required for all API operations.
    /// </value>
    [Required(ErrorMessage = "Gemini API key is required")]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or initializes the Gemini model identifier to use for text generation.
    /// </summary>
    /// <value>
    ///     The model name. Defaults to "gemini-2.0-flash" for optimal balance of speed and quality.
    /// </value>
    /// <remarks>
    ///     Available models include "gemini-2.0-flash", "gemini-1.5-pro", and "gemini-1.5-flash".
    ///     See Google AI documentation for current model availability and capabilities.
    /// </remarks>
    public string Model { get; init; } = "gemini-2.0-flash";

    /// <summary>
    ///     Gets or initializes the timeout duration in seconds for HTTP client requests.
    /// </summary>
    /// <value>
    ///     The timeout in seconds. Must be between 5 and 120. Defaults to 30.
    /// </value>
    /// <remarks>
    ///     Resilience (retries, circuit breaker) is handled automatically by Microsoft.Extensions.Http.Resilience.
    /// </remarks>
    [Range(5, 120, ErrorMessage = "TimeoutSeconds must be between 5 and 120")]
    public int TimeoutSeconds { get; init; } = 30;
}
