namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

internal static class SseTestHelpers
{
    public static TestServer CreateSseTestServer<T>(ISseStream<T> sseStream,
        Action<IEndpointRouteBuilder> configureEndpoints) where T : class
    {
        var hostBuilder = new HostBuilder().ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices(services =>
            {
                services.AddSingleton(sseStream);
                services.AddRouting();
            });
            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(configureEndpoints);
            });
        });

        var host = hostBuilder.StartAsync().GetAwaiter().GetResult();
        return host.GetTestServer();
    }
}
