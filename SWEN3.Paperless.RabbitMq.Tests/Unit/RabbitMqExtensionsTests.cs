namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class RabbitMqExtensionsTests
{
    [Fact]
    public void AddPaperlessRabbitMq_WithValidConfig_ShouldRegisterAllServicesWithoutConnecting()
    {
        var services = new ServiceCollection();
        var connectionFactoryMock = new Mock<IConnectionFactory>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Uri"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        services.AddLogging();
        services.AddPaperlessRabbitMq(configuration, includeOcrResultStream: false, includeGenAiResultStream: false);
        services.AddSingleton<IConnectionFactory>(_ => connectionFactoryMock.Object);
        services.AddSingleton<IConnection>(_ => Mock.Of<IConnection>());
        services.AddSingleton<IChannel>(_ => Mock.Of<IChannel>());

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IConnection>().Should().NotBeNull();
        provider.GetRequiredService<IRabbitMqPublisher>().Should().NotBeNull();
        provider.GetRequiredService<IRabbitMqConsumerFactory>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().ContainSingle(s => s is RabbitMqTopologySetup);
        provider.GetService<ISseStream<OcrEvent>>().Should().BeNull();
        provider.GetService<ISseStream<GenAIEvent>>().Should().BeNull();
    }

    [Fact]
    public void AddPaperlessRabbitMq_WithMissingUri_ShouldThrow()
    {
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder().Build();

        var act = () => services.AddPaperlessRabbitMq(emptyConfig);

        act.Should().Throw<InvalidOperationException>().WithMessage("Configuration value 'RabbitMQ:Uri' is missing");
    }

    [Fact]
    public void AddPaperlessRabbitMq_WithSseStreamsEnabled_ShouldRegisterStreams()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Uri"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        services.AddPaperlessRabbitMq(configuration, includeOcrResultStream: true, includeGenAiResultStream: true);

        var provider = services.BuildServiceProvider();
        provider.GetService<ISseStream<OcrEvent>>().Should().NotBeNull();
        provider.GetService<ISseStream<GenAIEvent>>().Should().NotBeNull();
    }
}
