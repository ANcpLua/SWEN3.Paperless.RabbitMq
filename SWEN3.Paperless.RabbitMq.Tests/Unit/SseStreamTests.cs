namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class SseStreamTests
{
    private readonly SseStream<Messages.SimpleMessage> _sseStream = new();

    [Fact]
    public void Subscribe_ShouldReturnChannelReader()
    {
        var reader = _sseStream.Subscribe(Guid.NewGuid());

        reader.Should().NotBeNull();
        reader.Should().BeAssignableTo<ChannelReader<Messages.SimpleMessage>>();
    }

    [Fact]
    public async Task Publish_ToSubscribedClient_ShouldReceiveMessage()
    {
        var reader = _sseStream.Subscribe(Guid.NewGuid());
        var message = new Messages.SimpleMessage(1);

        _sseStream.Publish(message);

        var received = await reader.ReadAsync(TestContext.Current.CancellationToken);
        received.Should().BeEquivalentTo(message);
    }

    [Fact]
    public async Task Publish_ToMultipleClients_ShouldBroadcast()
    {
        var reader1 = _sseStream.Subscribe(Guid.NewGuid());
        var reader2 = _sseStream.Subscribe(Guid.NewGuid());
        var message = new Messages.SimpleMessage(1);

        _sseStream.Publish(message);

        var received1 = await reader1.ReadAsync(TestContext.Current.CancellationToken);
        var received2 = await reader2.ReadAsync(TestContext.Current.CancellationToken);

        received1.Should().BeEquivalentTo(message);
        received2.Should().BeEquivalentTo(message);
    }

    [Fact]
    public async Task Unsubscribe_ShouldCloseChannel()
    {
        var clientId = Guid.NewGuid();
        var reader = _sseStream.Subscribe(clientId);

        _sseStream.Unsubscribe(clientId);

        await Assert.ThrowsAsync<ChannelClosedException>(() =>
            reader.ReadAsync(TestContext.Current.CancellationToken).AsTask());
    }
}