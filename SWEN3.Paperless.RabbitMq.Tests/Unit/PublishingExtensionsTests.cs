namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class PublishingExtensionsTests
{
    private readonly Mock<IRabbitMqPublisher> _publisherMock = new();

    [Fact]
    public async Task PublishOcrCommandAsync_ShouldPublishWithCorrectRoutingKey()
    {
        var command = new OcrCommand(Guid.NewGuid(), "document.pdf", "/path/to/document.pdf");

        await _publisherMock.Object.PublishOcrCommandAsync(command);

        _publisherMock.Verify(p => p.PublishAsync(RabbitMqSchema.OcrCommandRouting, command), Times.Once);
    }

    [Fact]
    public async Task PublishOcrEventAsync_ShouldPublishWithCorrectRoutingKey()
    {
        var @event = new OcrEvent(Guid.NewGuid(), "Completed", "Extracted text", DateTimeOffset.UtcNow);

        await _publisherMock.Object.PublishOcrEventAsync(@event);

        _publisherMock.Verify(p => p.PublishAsync(RabbitMqSchema.OcrEventRouting, @event), Times.Once);
    }

    [Fact]
    public async Task PublishOcrCommandAsync_WhenPublisherThrows_ShouldPropagateException()
    {
        var command = new OcrCommand(Guid.NewGuid(), "test.pdf", "/test.pdf");
        _publisherMock.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OcrCommand>()))
            .ThrowsAsync(new InvalidOperationException("Publishing failed"));

        var act = async () => await _publisherMock.Object.PublishOcrCommandAsync(command);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Publishing failed");
    }
}