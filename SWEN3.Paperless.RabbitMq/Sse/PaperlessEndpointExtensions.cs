using Microsoft.AspNetCore.Routing;
using SWEN3.Paperless.RabbitMq.Models;

namespace SWEN3.Paperless.RabbitMq.Sse;

/// <summary>
///     Extension methods for mapping Paperless-specific endpoints.
/// </summary>
public static class PaperlessEndpointExtensions
{
    /// <summary>
    ///     Maps the OCR event stream endpoint for real-time updates via Server-Sent Events.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="pattern">The endpoint pattern (default: "/api/v1/ocr-results").</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    /// <example>
    ///     <code>
    /// // Use default endpoint
    /// app.MapOcrEventStream();
    ///  <br>
    ///         </br>
    /// // Or with custom endpoint
    /// app.MapOcrEventStream("/custom/ocr-stream");
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapOcrEventStream(this IEndpointRouteBuilder app,
        string pattern = "/api/v1/ocr-results")
    {
        app.MapSse<OcrEvent>(pattern, result => new
                { jobId = result.JobId, status = result.Status, text = result.Text, processedAt = result.ProcessedAt },
            result => result.Status is "Completed" ? "ocr-completed" : "ocr-failed");

        return app;
    }
}