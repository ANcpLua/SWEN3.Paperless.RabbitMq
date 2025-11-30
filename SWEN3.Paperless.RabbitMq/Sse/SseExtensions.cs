using System.Runtime.CompilerServices;
using System.Text.Json;
#if NET10_0_OR_GREATER
using System.Net.ServerSentEvents;
#endif
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
    ///     Registers the necessary services for Server-Sent Events (SSE) streaming into the dependency injection container.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of events to be streamed. Must be a reference type (class).
    /// </typeparam>
    /// <param name="services">
    ///     The <see cref="IServiceCollection"/> to add the services to.
    /// </param>
    /// <returns>
    ///     The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         Registers <see cref="ISseStream{T}"/> as a singleton, ensuring a single shared stream instance
    ///         manages subscriptions for all clients.
    ///     </para>
    ///     <para>
    ///         <strong>Note:</strong> If you are using <c>AddPaperlessRabbitMq</c>, the OCR event stream is automatically
    ///         registered when <c>includeOcrResultStream</c> is set to <see langword="true"/>.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///         // Register a custom event stream
    ///         services.AddSseStream&lt;MyCustomEvent&gt;();
    ///     </code>
    /// </example>
    public static IServiceCollection AddSseStream<T>(this IServiceCollection services) where T : class
    {
        services.AddSingleton<ISseStream<T>, SseStream<T>>();
        return services;
    }

    /// <summary>
    ///     Maps a GET endpoint that streams Server-Sent Events (SSE) to connected clients.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of the event object being streamed.
    /// </typeparam>
    /// <param name="endpoints">
    ///     The <see cref="IEndpointRouteBuilder"/> to add the route to.
    /// </param>
    /// <param name="pattern">
    ///     The URL pattern for the endpoint (e.g., <c>"/api/events"</c>).
    /// </param>
    /// <param name="payloadSelector">
    ///     A function that projects the internal event object <typeparamref name="T"/> into the payload object
    ///     that will be serialized to JSON and sent to the client.
    /// </param>
    /// <param name="eventTypeSelector">
    ///     A function that determines the SSE "event" type string (e.g., "message", "update") for the client to listen for.
    /// </param>
    /// <returns>
    ///     A <see cref="RouteHandlerBuilder"/> that can be used to further configure the endpoint (e.g., adding authorization).
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         This method handles the entire lifecycle of an SSE connection:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><description>Generates a unique Client ID for the connection.</description></item>
    ///         <item><description>Subscribes the client to the <see cref="ISseStream{T}"/>.</description></item>
    ///         <item><description>Streams events asynchronously as they are published.</description></item>
    ///         <item><description>Automatically unsubscribes and cleans up resources when the client disconnects.</description></item>
    ///     </list>
    /// </remarks>
    public static RouteHandlerBuilder MapSse<T>(this IEndpointRouteBuilder endpoints, string pattern,
        Func<T, object> payloadSelector, Func<T, string> eventTypeSelector) where T : class
    {
#if NET10_0_OR_GREATER
        return MapSseNet10(endpoints, pattern, payloadSelector, eventTypeSelector);
#else
        return MapSseFallback(endpoints, pattern, payloadSelector, eventTypeSelector);
#endif
    }

    /// <summary>
    ///     Maps a GET endpoint that streams Server-Sent Events (SSE) to connected clients,
    ///     serializing the entire event object as the payload using camelCase naming.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of the event object being streamed.
    /// </typeparam>
    /// <param name="endpoints">
    ///     The <see cref="IEndpointRouteBuilder"/> to add the route to.
    /// </param>
    /// <param name="pattern">
    ///     The URL pattern for the endpoint (e.g., <c>"/api/events"</c>).
    /// </param>
    /// <param name="eventTypeSelector">
    ///     A function that determines the SSE "event" type string (e.g., "message", "update") for the client to listen for.
    /// </param>
    /// <returns>
    ///     A <see cref="RouteHandlerBuilder"/> that can be used to further configure the endpoint (e.g., adding authorization).
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         This is a simplified overload that serializes the entire event object as JSON.
    ///         Use this when you don't need to transform the event before sending.
    ///     </para>
    ///     <para>
    ///         The event object is serialized using <see cref="JsonSerializerDefaults.Web"/> (camelCase property naming).
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///         app.MapSse&lt;NotificationEvent&gt;("/api/notifications", evt => evt.Type);
    ///     </code>
    /// </example>
    public static RouteHandlerBuilder MapSse<T>(this IEndpointRouteBuilder endpoints, string pattern,
        Func<T, string> eventTypeSelector) where T : class
    {
        return endpoints.MapSse(pattern, static item => item, eventTypeSelector);
    }

#if NET10_0_OR_GREATER
    /// <summary>
    ///     .NET 10+ implementation using native ServerSentEvents API.
    ///     This method is only compiled on .NET 10+ and is excluded from coverage on other frameworks.
    /// </summary>
    private static RouteHandlerBuilder MapSseNet10<T>(
        IEndpointRouteBuilder endpoints,
        string pattern,
        Func<T, object> payloadSelector,
        Func<T, string> eventTypeSelector) where T : class
    {
        return endpoints.MapGet(pattern, (ISseStream<T> stream, HttpContext context) =>
        {
            var clientId = Guid.NewGuid();
            var reader = stream.Subscribe(clientId);
            context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

            return TypedResults.ServerSentEvents(StreamEventsAsync(context.RequestAborted));

            async IAsyncEnumerable<SseItem<object>> StreamEventsAsync([EnumeratorCancellation] CancellationToken ct)
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

#endif

#if !NET10_0_OR_GREATER
    /// <summary>
    ///     .NET 8/9 fallback implementation with manual SSE formatting.
    ///     This method is only compiled on .NET 8/9 and is excluded from coverage on .NET 10+.
    /// </summary>
    private static RouteHandlerBuilder MapSseFallback<T>(
        IEndpointRouteBuilder endpoints,
        string pattern,
        Func<T, object> payloadSelector,
        Func<T, string> eventTypeSelector) where T : class
    {
        return endpoints.MapGet(pattern, async (ISseStream<T> stream, HttpContext context, CancellationToken ct) =>
        {
            var clientId = Guid.NewGuid();
            var reader = stream.Subscribe(clientId);
            try
            {
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";
                context.Response.Headers.ContentType = "text/event-stream";

                // Start the response so headers are sent immediately, even before the first event
                await context.Response.StartAsync(ct).ConfigureAwait(false);

                await foreach (var item in reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    var payload = payloadSelector(item);
                    var eventType = eventTypeSelector(item);
                    var json = JsonSerializer.Serialize(payload);
                    await context.Response.WriteAsync($"event: {eventType}\n", ct).ConfigureAwait(false);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                stream.Unsubscribe(clientId);
            }
        });
    }
#endif
}
