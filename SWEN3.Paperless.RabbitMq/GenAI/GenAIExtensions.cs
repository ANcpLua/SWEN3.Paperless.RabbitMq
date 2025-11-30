using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Provides extension methods to configure GenAI document summarization services.
///     <para>Use <see cref="GenAIExtensions.AddPaperlessGenAI" /> to register GenAI services with automatic resilience.</para>
/// </summary>
public static class GenAIExtensions
{
    /// <summary>
    ///     Adds GenAI document summarization services with Google Gemini integration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration containing Gemini settings under "Gemini" section.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    ///     <para>This method configures:</para>
    ///     <list type="bullet">
    ///         <item>Gemini API options from configuration (ApiKey, Model, TimeoutSeconds)</item>
    ///         <item>HttpClient for <see cref="GeminiService" /> with automatic resilience (retry, circuit breaker, timeout)</item>
    ///         <item>Background worker for processing GenAI commands from RabbitMQ</item>
    ///     </list>
    ///     <para>Resilience is provided by Microsoft.Extensions.Http.Resilience with standard defaults:</para>
    ///     <list type="bullet">
    ///         <item>3 retry attempts with exponential backoff + jitter</item>
    ///         <item>Circuit breaker for preventing cascade failures</item>
    ///         <item>Automatic handling of transient HTTP errors (5xx, timeouts, rate limits)</item>
    ///     </list>
    /// </remarks>
    /// <example>
    ///     Configuration (appsettings.json):
    ///     <code>
    ///     {
    ///       "Gemini": {
    ///         "ApiKey": "your-api-key",
    ///         "Model": "gemini-2.5-flash",
    ///         "TimeoutSeconds": 30
    ///       }
    ///     }
    ///     </code>
    ///     Registration:
    ///     <code>
    ///     builder.Services.AddPaperlessGenAI(builder.Configuration);
    ///     </code>
    /// </example>
    public static IServiceCollection AddPaperlessGenAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        services.AddHttpClient<ITextSummarizer, GeminiService>()
            .AddStandardResilienceHandler();

        services.AddHostedService<GenAIWorker>();

        return services;
    }
}
