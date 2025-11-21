namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class RabbitMqConsumerFactoryTests
{
    private readonly Mock<IConnection> _connectionMock = new();
    private readonly Mock<IChannel> _channelMock = new();
    private readonly RabbitMqConsumerFactory _factory;

    public RabbitMqConsumerFactoryTests()
    {
        _connectionMock.Setup(c => c.CreateChannelAsync(options: null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channelMock.Object);
        _channelMock.Setup(c => c.BasicQosAsync(0, 1, global: false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _channelMock.Setup(c => c.DisposeAsync()).Returns(new ValueTask());
        _factory = new RabbitMqConsumerFactory(_connectionMock.Object);
    }

    [Fact]
    public async Task CreateConsumerAsync_ShouldApplyQosAndReturnConsumer()
    {
        await using var consumer = await _factory.CreateConsumerAsync<Messages.SimpleMessage>();

        _connectionMock.Verify(c => c.CloseReason, Times.Once);
        _connectionMock.Verify(c => c.CreateChannelAsync(options: null, It.IsAny<CancellationToken>()), Times.Once);
        _channelMock.Verify(c => c.CloseReason, Times.Once);
        _channelMock.Verify(c => c.BasicQosAsync(0, 1, global: false, It.IsAny<CancellationToken>()), Times.Once);
        consumer.Should().BeOfType<RabbitMqConsumer<Messages.SimpleMessage>>();
    }

    [Fact]
    public async Task CreateConsumerAsync_ShouldDeriveQueueNameFromType()
    {
        await using var consumer =
            (RabbitMqConsumer<Messages.SimpleMessage>)await _factory.CreateConsumerAsync<Messages.SimpleMessage>();

        GetQueueName(consumer).Should().Be("SimpleMessageQueue");
    }

    [Fact]
    public async Task CreateConsumerAsync_ShouldForwardCancellationTokens()
    {
        var connectionShutdown = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "closing");
        var channelShutdown = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "channel closing");
        _connectionMock.Setup(c => c.CloseReason).Returns(connectionShutdown);
        _channelMock.Setup(c => c.CloseReason).Returns(channelShutdown);
        _connectionMock.Setup(c => c.CreateChannelAsync(options: null, connectionShutdown.CancellationToken))
            .ReturnsAsync(_channelMock.Object);
        _channelMock.Setup(c => c.BasicQosAsync(0, 1, global: false, channelShutdown.CancellationToken))
            .Returns(Task.CompletedTask);

        await using var _ = await _factory.CreateConsumerAsync<Messages.SimpleMessage>();

        _connectionMock.Verify(c => c.CreateChannelAsync(options: null, connectionShutdown.CancellationToken), Times.Once);
        _channelMock.Verify(c => c.BasicQosAsync(0, 1, global: false, channelShutdown.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task CreateConsumerAsync_ShouldUseNoneTokensWhenCloseReasonsAreNull()
    {
        _connectionMock.Setup(c => c.CreateChannelAsync(options: null, CancellationToken.None))
            .ReturnsAsync(_channelMock.Object);
        _channelMock.Setup(c => c.CloseReason).Returns((ShutdownEventArgs?)null);
        _channelMock.Setup(c => c.BasicQosAsync(0, 1, global: false, CancellationToken.None))
            .Returns(Task.CompletedTask);

        await using var _ = await _factory.CreateConsumerAsync<Messages.SimpleMessage>();

        _connectionMock.Verify(c => c.CreateChannelAsync(options: null, CancellationToken.None), Times.Once);
        _channelMock.Verify(c => c.BasicQosAsync(0, 1, global: false, CancellationToken.None), Times.Once);
    }

    private static string GetQueueName(RabbitMqConsumer<Messages.SimpleMessage> consumer) =>
        (string)typeof(RabbitMqConsumer<Messages.SimpleMessage>)
            .GetField("_queueName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(consumer)!;
}
