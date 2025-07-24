using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using SWEN3.Paperless.RabbitMq.Consuming;
using SWEN3.Paperless.RabbitMq.Internal;
using SWEN3.Paperless.RabbitMq.Models;
using SWEN3.Paperless.RabbitMq.Publishing;
using SWEN3.Paperless.RabbitMq.Sse;

namespace SWEN3.Paperless.RabbitMq;

/// <summary>
///     <para>Use <see cref="PublishingExtensions.PublishOcrCommandAsync{T}" /> to publish OCR commands.</para>
///     <para>Use <see cref="PublishingExtensions.PublishOcrEventAsync{T}" /> to publish OCR events.</para>
///     <para>Use <see cref="IRabbitMqConsumerFactory.CreateConsumerAsync{T}" /> to create message consumers.</para>
///     <para>Use <see cref="PaperlessEndpointExtensions.MapOcrEventStream" /> to map SSE endpoints.</para>
/// </summary>
public static class RabbitMqExtensions
{
    private const string DefaultConfigSection = "RabbitMQ:Uri";

    /// <summary>
    ///     Adds RabbitMQ messaging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration containing RabbitMQ settings.</param>
    /// <param name="includeOcrResultStream">Whether to include Server-Sent Events streaming for OCR results.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when RabbitMQ URI is not configured.</exception>
    /// <example>
    ///     Basic usage (no SSE streaming):
    ///     <code>builder.Services.AddPaperlessRabbitMq(builder.Configuration);</code>
    ///     <para />
    ///     With OCR event streaming enabled:
    ///     <code>services.AddPaperlessRabbitMq(config, includeOcrResultStream: true);</code>
    /// </example>
    public static IServiceCollection AddPaperlessRabbitMq(this IServiceCollection services,
        IConfiguration configuration, bool includeOcrResultStream = false)
    {
        var uri = configuration[DefaultConfigSection] ??
                  throw new InvalidOperationException($"Configuration value '{DefaultConfigSection}' is missing");

        var factory = new ConnectionFactory { Uri = new Uri(uri) };
        var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();

        services.AddSingleton(connection);
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddSingleton<IRabbitMqConsumerFactory, RabbitMqConsumerFactory>();
        services.AddHostedService<RabbitMqTopologySetup>();

        if (includeOcrResultStream) services.AddSseStream<OcrEvent>();

        return services;
    }
}