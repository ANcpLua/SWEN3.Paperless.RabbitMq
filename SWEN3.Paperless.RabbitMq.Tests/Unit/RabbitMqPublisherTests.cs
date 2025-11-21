namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class RabbitMqPublisherTests
{
    private readonly Mock<IChannel> _channelMock = new();
    private readonly Mock<IConnection> _connectionMock = new();
    private readonly RabbitMqPublisher _publisher;

    public RabbitMqPublisherTests()
    {
        _connectionMock.Setup(c => c.CreateChannelAsync(options: null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channelMock.Object);
        _channelMock.Setup(c => c.DisposeAsync()).Returns(new ValueTask());
        _publisher = new RabbitMqPublisher(_connectionMock.Object);
    }

    [Fact]
    public async Task PublishAsync_ShouldCreateAndDisposeChannel()
    {
        await _publisher.PublishAsync("test.key", new { Id = 1 });

        _connectionMock.Verify(c => c.CreateChannelAsync(options: null, It.IsAny<CancellationToken>()), Times.Once);
        _channelMock.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenChannelCreationFails_ShouldThrow()
    {
        var failingConnection = new Mock<IConnection>();
        failingConnection.Setup(c => c.CreateChannelAsync(options: null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection closed"));
        var publisher = new RabbitMqPublisher(failingConnection.Object);

        var act = async () => await publisher.PublishAsync("test.key", new { Id = 1 });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Connection closed");
    }

    [Fact]
    public async Task PublishAsync_ShouldCheckConnectionCloseReason()
    {
        await _publisher.PublishAsync("test.key", new { Id = 1 });

        _connectionMock.Verify(c => c.CloseReason, Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldCheckChannelCloseReason()
    {
        await _publisher.PublishAsync("test.key", new { Id = 1 });

        _channelMock.Verify(c => c.CloseReason, Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldForwardCancellationTokens()
    {
        var connectionShutdown = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "closing");
        var channelShutdown = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "channel closing");
        _connectionMock.Setup(c => c.CloseReason).Returns(connectionShutdown);
        _channelMock.Setup(c => c.CloseReason).Returns(channelShutdown);
        _connectionMock.Setup(c => c.CreateChannelAsync(options: null, connectionShutdown.CancellationToken))
            .ReturnsAsync(_channelMock.Object);

        await _publisher.PublishAsync("test.key", new { Id = 1 });

        _connectionMock.Verify(c => c.CreateChannelAsync(options: null, connectionShutdown.CancellationToken), Times.Once);
        _connectionMock.Verify(c => c.CloseReason, Times.AtLeastOnce);
        _channelMock.Verify(c => c.CloseReason, Times.AtLeastOnce);
    }
}
