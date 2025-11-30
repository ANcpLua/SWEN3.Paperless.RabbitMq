namespace SWEN3.Paperless.RabbitMq.Tests.Helpers;

/// <summary>
///     Helpers for creating SSE test servers using modern generic host builder.
/// </summary>
internal static class SseTestHelpers
{
    /// <summary>
    ///     Default polling interval for client connection checks.
    /// </summary>
    private const int PollingIntervalMs = 50;

    /// <summary>
    ///     Default timeout for waiting for clients to connect.
    /// </summary>
    private static readonly TimeSpan DefaultClientTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Default stabilization delay after client connects.
    /// </summary>
    private static readonly TimeSpan ConnectionStabilizationDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     Waits for expected number of clients to connect to the SSE stream using linked cancellation tokens.
    /// </summary>
    /// <typeparam name="T">The event type of the stream</typeparam>
    /// <param name="stream">The SSE stream to monitor</param>
    /// <param name="expectedClients">Number of clients to wait for (default: 1)</param>
    /// <param name="timeout">Timeout duration (default: 10 seconds)</param>
    /// <param name="stabilize">Whether to add stabilization delay after connection (default: true)</param>
    /// <param name="cancellationToken">Parent cancellation token</param>
    /// <exception cref="TimeoutException">Thrown when clients don't connect within timeout</exception>
    public static async Task WaitForClientsAsync<T>(
        ISseStream<T> stream,
        int expectedClients = 1,
        TimeSpan? timeout = null,
        bool stabilize = true,
        CancellationToken cancellationToken = default) where T : class
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? DefaultClientTimeout);

        try
        {
            while (stream.ClientCount < expectedClients)
            {
                await Task.Delay(PollingIntervalMs, timeoutCts.Token);
            }

            if (stabilize)
            {
                await Task.Delay(ConnectionStabilizationDelay, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Expected {expectedClients} client(s) but only {stream.ClientCount} connected within timeout");
        }
    }

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
