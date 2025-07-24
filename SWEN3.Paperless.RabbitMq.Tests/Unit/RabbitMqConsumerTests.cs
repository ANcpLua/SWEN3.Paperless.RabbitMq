namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class RabbitMqConsumerTests
{
    private readonly Mock<IChannel> _channelMock = new();

    [Fact]
    public async Task ProcessMessageAsync_WithInvalidJson_ShouldNack()
    {
        var consumer = new RabbitMqConsumer<Messages.SimpleMessage>(_channelMock.Object, "test");
        var invalidJson = "{ invalid json }"u8.ToArray();
        var eventArgs = new BasicDeliverEventArgs("tag", 123, false, "exchange", "key", null!, invalidJson);
        var channel = Channel.CreateUnbounded<(Messages.SimpleMessage, ulong)>();

        await consumer.ProcessMessageAsync(eventArgs, channel.Writer, CancellationToken.None);

        _channelMock.Verify(c => c.BasicNackAsync(123, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithValidMessage_ShouldWriteToChannel()
    {
        var consumer = new RabbitMqConsumer<Messages.SimpleMessage>(_channelMock.Object, "test");
        var testMessage = new Messages.SimpleMessage(42);
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(testMessage);
        var eventArgs = new BasicDeliverEventArgs("tag", 456, false, "exchange", "key", null!, messageBytes);
        var channel = Channel.CreateUnbounded<(Messages.SimpleMessage, ulong)>();

        await consumer.ProcessMessageAsync(eventArgs, channel.Writer, CancellationToken.None);

        var hasMessage = channel.Reader.TryRead(out var result);
        hasMessage.Should().BeTrue();
        result.Item1.Should().BeEquivalentTo(testMessage);
        result.Item2.Should().Be(456);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithNull_ShouldNotWriteToChannel()
    {
        var consumer = new RabbitMqConsumer<Messages.SimpleMessage>(_channelMock.Object, "test");
        var nullJson = "null"u8.ToArray();
        var eventArgs = new BasicDeliverEventArgs("tag", 789, false, "exchange", "key", null!, nullJson);
        var channel = Channel.CreateUnbounded<(Messages.SimpleMessage, ulong)>();

        await consumer.ProcessMessageAsync(eventArgs, channel.Writer, CancellationToken.None);

        channel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task AckAsync_WithoutDeliveryTag_ShouldNotCallChannel()
    {
        var consumer = new RabbitMqConsumer<Messages.SimpleMessage>(_channelMock.Object, "test");

        await consumer.AckAsync();

        _channelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NackAsync_WithoutDeliveryTag_ShouldNotCallChannel()
    {
        var consumer = new RabbitMqConsumer<Messages.SimpleMessage>(_channelMock.Object, "test");

        await consumer.NackAsync();

        _channelMock.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeChannel()
    {
        var consumer = new RabbitMqConsumer<Messages.SimpleMessage>(_channelMock.Object, "test");

        await consumer.DisposeAsync();

        _channelMock.Verify(c => c.DisposeAsync(), Times.Once);
    }
}
