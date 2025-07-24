namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class RabbitMqExtensionsTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();
    private IConfiguration _configuration = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Uri"] = _container.GetConnectionString()
            })
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public void AddPaperlessRabbitMq_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPaperlessRabbitMq(_configuration);

        var provider = services.BuildServiceProvider();
        provider.GetService<IConnection>().Should().NotBeNull();
        provider.GetService<IRabbitMqPublisher>().Should().NotBeNull();
        provider.GetService<IRabbitMqConsumerFactory>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().ContainSingle(s => s is RabbitMqTopologySetup);
    }

    [Fact]
    public void AddPaperlessRabbitMq_WithMissingUri_ShouldThrow()
    {
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder().Build();

        var act = () => services.AddPaperlessRabbitMq(emptyConfig);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Configuration value 'RabbitMQ:Uri' is missing");
    }

    [Fact]
    public void AddPaperlessRabbitMq_WithOcrStream_ShouldRegisterSseStream()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPaperlessRabbitMq(_configuration, true);

        var provider = services.BuildServiceProvider();
        provider.GetService<ISseStream<OcrEvent>>().Should().NotBeNull();
    }

    [Fact]
    public async Task TopologySetup_ShouldCreateExchangesAndQueues()
    {
        var services = new ServiceCollection();
        services.AddPaperlessRabbitMq(_configuration);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>()
            .OfType<RabbitMqTopologySetup>()
            .First();

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        var connection = provider.GetRequiredService<IConnection>();
        connection.IsOpen.Should().BeTrue();
    }
}