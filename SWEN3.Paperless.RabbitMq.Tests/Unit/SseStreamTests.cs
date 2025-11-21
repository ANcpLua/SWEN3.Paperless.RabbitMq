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

    [Fact]
    public void Publish_WhenClientIsSlow_ShouldDropOldestMessages()
    {
        // Arrange
        const int MessagesToSend = 110; // Capacity is 100, so 10 should be dropped
        const int ExpectedFirstMessageId = 11; // 1-10 are dropped

        var reader = _sseStream.Subscribe(Guid.NewGuid());

        // Act: Publish more messages than the channel capacity without reading
        for (var i = 1; i <= MessagesToSend; i++)
        {
            _sseStream.Publish(new Messages.SimpleMessage(i));
        }

        // Assert: Read all available messages
        var receivedMessages = new List<Messages.SimpleMessage>();

        // Read until empty (try-read pattern is safer than assuming exact count due to concurrency)
        while (reader.TryRead(out var msg))
        {
            receivedMessages.Add(msg);
        }

        // Since it's a bounded channel, reader might not be empty immediately if we were reading in parallel,
        // but here we are single-threaded in test.
        // However, strictly speaking, TryRead returns what's currently available.

        receivedMessages.Should().HaveCount(100); // Should match capacity
        receivedMessages[0].Id.Should().Be(ExpectedFirstMessageId); // Should start from 11
        receivedMessages[^1].Id.Should().Be(MessagesToSend); // Should end at 110
    }
}
