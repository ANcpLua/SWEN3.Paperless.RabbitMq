namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

internal static class SseTestHelpers
{
    public static TestServer CreateSseTestServer<T>(ISseStream<T> sseStream,
        Action<IEndpointRouteBuilder> configureEndpoints) where T : class
    {
        var builder = new WebHostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(sseStream);
            services.AddRouting();
        }).Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(configureEndpoints);
        });

        return new TestServer(builder);
    }
}