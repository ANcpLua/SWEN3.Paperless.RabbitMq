namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

/// <summary>
///     Helpers for creating SSE test servers using modern generic host builder.
/// </summary>
internal static class SseTestHelpers
{
    /// <summary>
    ///     Creates a test server with SSE stream configured, using modern IHost and TestServer.
    /// </summary>
    /// <typeparam name="T">The event type to stream</typeparam>
    /// <param name="configureServices">Optional additional service configuration</param>
    /// <param name="configureEndpoints">Endpoint configuration (e.g., MapSse calls)</param>
    /// <returns>A tuple of (IHost, TestServer) for use in tests</returns>
    public static async Task<(IHost Host, TestServer Server)> CreateSseTestServerAsync<T>(
        Action<IServiceCollection>? configureServices,
        Action<IEndpointRouteBuilder> configureEndpoints) where T : class
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSseStream<T>();
                    configureServices?.Invoke(services);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(configureEndpoints);
                });
            });

        var host = await hostBuilder.StartAsync();
        return (host, host.GetTestServer());
    }
}
