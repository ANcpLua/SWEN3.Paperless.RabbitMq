namespace SWEN3.Paperless.RabbitMq.Tests.Integration;

public class SseExtensionsTests
{
    [Fact]
    public async Task MapSse_WhenClientDisconnects_ShouldUnsubscribe()
    {
        var mockStream = new Mock<ISseStream<Messages.SseTestEvent>>();
        var clientId = Guid.Empty;
        var subscribedTcs = new TaskCompletionSource();
        var unsubscribedTcs = new TaskCompletionSource();

        mockStream.Setup(s => s.Subscribe(It.IsAny<Guid>()))
            .Callback<Guid>(id =>
            {
                clientId = id;
                subscribedTcs.SetResult();
            })
            .Returns(Channel.CreateUnbounded<Messages.SseTestEvent>().Reader);

        mockStream.Setup(s => s.Unsubscribe(It.IsAny<Guid>()))
            .Callback<Guid>(id =>
            {
                if (id == clientId) unsubscribedTcs.SetResult();
            });

        using var server = TestServerFactory.CreateSseTestServerWithTestEndpoint(mockStream.Object);

        using var client = server.CreateClient();
        using var cts = new CancellationTokenSource();

        var request = client.GetAsync("/sse-test", cts.Token);
        await subscribedTcs.Task;

        await cts.CancelAsync();
        await unsubscribedTcs.Task;

        await Assert.ThrowsAsync<TaskCanceledException>(() => request);

        clientId.Should().NotBe(Guid.Empty);
        mockStream.Verify(s => s.Unsubscribe(clientId), Times.Once);
    }

    [Fact]
    public async Task MapSse_WithPublishedEvent_ShouldStreamToClient()
    {
        var mockStream = new Mock<ISseStream<Messages.SseTestEvent>>();
        var subscriptionTcs = new TaskCompletionSource();
        var channel = Channel.CreateUnbounded<Messages.SseTestEvent>();

        mockStream.Setup(s => s.Subscribe(It.IsAny<Guid>()))
            .Callback(() => subscriptionTcs.TrySetResult())
            .Returns(channel.Reader);

        using var server = TestServerFactory.CreateSseTestServerWithTestEndpoint(mockStream.Object);

        using var client = server.CreateClient();

        var responseTask = client.GetAsync("/sse-test", HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);
        await subscriptionTcs.Task;

        await channel.Writer.WriteAsync(new Messages.SseTestEvent { Id = 42, Message = "Hello SSE" },
            TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        channel.Writer.TryComplete();

        var response = await responseTask;
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(responseStream);

        var eventTypeLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        var dataLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        var emptyLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

        eventTypeLine.Should().Be("event: test-event");
        dataLine.Should().StartWith("data: ")
            .And.Contain("\"id\":42")
            .And.Contain("\"message\":\"Hello SSE\"");
        emptyLine.Should().BeEmpty();
    }
}