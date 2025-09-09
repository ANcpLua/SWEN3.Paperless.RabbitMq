namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GenAIPublishingExtensionsTests
{
    private readonly Mock<IRabbitMqPublisher> _publisherMock = new();

    [Fact]
    public async Task PublishGenAICommandAsync_CallsPublishWithCorrectRouting()
    {
        var publisherMock = new Mock<IRabbitMqPublisher>();
        var command = new GenAICommand(Guid.NewGuid(), "Test text");

        await publisherMock.Object.PublishGenAICommandAsync(command);

        publisherMock.Verify(p => p.PublishAsync(RabbitMqSchema.GenAICommandRouting, command), Times.Once);
    }

    [Fact]
    public async Task PublishGenAIEventAsync_ShouldPublishWithCorrectRoutingKey()
    {
        var genAiEvent = new GenAIEvent(Guid.NewGuid(), "This is a test summary", DateTimeOffset.UtcNow);
        await _publisherMock.Object.PublishGenAIEventAsync(genAiEvent);
        _publisherMock.Verify(p => p.PublishAsync(RabbitMqSchema.GenAIEventRouting, genAiEvent), Times.Once);
    }

    [Fact]
    public async Task PublishGenAIEventAsync_WithError_ShouldPublishWithErrorMessage()
    {
        var genAiEvent = new GenAIEvent(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow,
            "Failed to generate summary");
        await _publisherMock.Object.PublishGenAIEventAsync(genAiEvent);
        _publisherMock.Verify(p => p.PublishAsync(RabbitMqSchema.GenAIEventRouting, genAiEvent), Times.Once);
    }

    [Fact]
    public async Task PublishGenAIEventAsync_WhenPublisherThrows_ShouldPropagateException()
    {
        var genAiEvent = new GenAIEvent(Guid.NewGuid(), "Test summary", DateTimeOffset.UtcNow);
        _publisherMock.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<GenAIEvent>()))
            .ThrowsAsync(new InvalidOperationException("Publishing failed"));
        var act = async () => await _publisherMock.Object.PublishGenAIEventAsync(genAiEvent);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Publishing failed");
    }
}