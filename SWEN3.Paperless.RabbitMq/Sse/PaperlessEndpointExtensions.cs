using Microsoft.AspNetCore.Routing;
using SWEN3.Paperless.RabbitMq.Models;

namespace SWEN3.Paperless.RabbitMq.Sse;

/// <summary>
///     Extension methods for mapping Paperless-specific endpoints.
/// </summary>
public static class PaperlessEndpointExtensions
{
    /// <summary>
    ///     Default endpoint patterns for Paperless SSE streams.
    /// </summary>
    private static class Endpoints
    {
        /// <summary>
        ///     Default endpoint for OCR event stream.
        /// </summary>
        public const string OcrEventStream = "/api/v1/ocr-results";

        /// <summary>
        ///     Default endpoint for GenAI event stream.
        /// </summary>
        public const string GenAIEventStream = "/api/v1/events/genai";
    }

    /// <summary>
    ///     Maps the OCR event stream endpoint for real-time updates via Server-Sent Events.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="pattern">The endpoint pattern (default: <see cref="Endpoints.OcrEventStream"/>).</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    /// <example>
    ///     <code>
    /// // Use default endpoint (/api/v1/ocr-results)
    /// app.MapOcrEventStream();
    ///   <br>
    ///         </br>
    /// // Or with custom endpoint
    /// app.MapOcrEventStream("/custom/ocr-stream");
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapOcrEventStream(this IEndpointRouteBuilder app,
        string pattern = Endpoints.OcrEventStream)
    {
        app.MapSse<OcrEvent>(pattern, result => new
        {
            jobId = result.JobId,
            status = result.Status,
            text = result.Text,
            processedAt = result.ProcessedAt
        }, result => result.Status is "Completed" ? "ocr-completed" : "ocr-failed");

        return app;
    }

    /// <summary>
    ///     Maps the GenAI event stream endpoint for real-time updates via Server-Sent Events.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="pattern">The endpoint pattern (default: <see cref="Endpoints.GenAIEventStream"/>).</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    /// <example>
    ///     <code>
    /// // Use default endpoint (/api/v1/events/genai)
    /// app.MapGenAIEventStream();
    ///  <br>
    ///         </br>
    /// // Or with custom endpoint
    /// app.MapGenAIEventStream("/custom/genai-stream");
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapGenAIEventStream(this IEndpointRouteBuilder app,
        string pattern = Endpoints.GenAIEventStream)
    {
        app.MapSse<GenAIEvent>(pattern, result => new
        {
            documentId = result.DocumentId,
            summary = result.Summary,
            generatedAt = result.GeneratedAt,
            errorMessage = result.ErrorMessage
        }, result => !string.IsNullOrEmpty(result.Summary) ? "genai-completed" : "genai-failed");

        return app;
    }
}