using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SWEN3.Paperless.RabbitMq.Sse;

/// <summary>
///     Extension methods for adding and mapping Server-Sent Events (SSE) streaming services.
/// </summary>
public static class SseExtensions
{
    /// <summary>
    ///     Adds Server-Sent Events streaming services to the dependency injection container.
    /// </summary>
    /// <typeparam name="T">The type of events to stream. Must be a class.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    ///     Note: For OCR events, this is automatically configured when calling
    ///     <see cref="RabbitMqExtensions.AddPaperlessRabbitMq" /> with includeOcrResultStream: true.
    /// </remarks>
    /// <example>
    ///     <code>services.AddSseStream&lt;MyCustomEvent&gt;();</code>
    /// </example>
    public static IServiceCollection AddSseStream<T>(this IServiceCollection services) where T : class
    {
        services.AddSingleton<ISseStream<T>, SseStream<T>>();
        return services;
    }

    /// <summary>
    ///     Maps a Server-Sent Events endpoint for real-time event streaming to connected clients.
    /// </summary>
    /// <typeparam name="T">The type of events to stream. Must be a class.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the SSE endpoint.</param>
    /// <param name="payloadSelector">Function to transform the event data for transmission.</param>
    /// <param name="eventTypeSelector">Function to determine the SSE event type name.</param>
    /// <returns>A route handler builder for further configuration.</returns>
    /// <remarks>
    ///     The endpoint automatically manages client subscriptions and handles disconnections.
    ///     Events are streamed as JSON-serialized payloads with custom event types.
    /// </remarks>
    internal static RouteHandlerBuilder MapSse<T>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<T, object> payloadSelector,
        Func<T, string> eventTypeSelector) where T : class
    {
        return endpoints.MapGet(pattern, (ISseStream<T> stream, HttpContext context) =>
        {
            var clientId = Guid.NewGuid();
            var reader = stream.Subscribe(clientId);
            context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

            return TypedResults.ServerSentEvents(StreamEvents(context.RequestAborted));

            async IAsyncEnumerable<SseItem<object>> StreamEvents([EnumeratorCancellation] CancellationToken ct)
            {
                await foreach (var item in reader.ReadAllAsync(ct))
                {
                    var payload = payloadSelector(item);
                    var eventType = eventTypeSelector(item);
                    yield return new SseItem<object>(payload, eventType);
                }
            }
        });
    }
}