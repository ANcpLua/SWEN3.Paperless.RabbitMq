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
    public async Task PublishOcrCommandAsync_WithCreatedAt_ShouldPublishWithTimestamp()
    {
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var command = new OcrCommand(Guid.NewGuid(), "document.pdf", "/path/to/document.pdf", createdAt);

        await _publisherMock.Object.PublishOcrCommandAsync(command);

        _publisherMock.Verify(p => p.PublishAsync(
            RabbitMqSchema.OcrCommandRouting,
            It.Is<OcrCommand>(c => c.CreatedAt == createdAt)), Times.Once);
    }

    [Fact]
    public void OcrCommand_WithoutCreatedAt_ShouldDefaultToNull()
    {
        var command = new OcrCommand(Guid.NewGuid(), "document.pdf", "/path/to/document.pdf");

        command.CreatedAt.Should().BeNull();
    }

    [Fact]
    public void OcrCommand_SerializesAndDeserializesCreatedAt()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        var original = new OcrCommand(Guid.NewGuid(), "test.pdf", "/test.pdf", createdAt);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OcrCommand>(json);

        deserialized.Should().NotBeNull();
        deserialized!.CreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void OcrCommand_DeserializesWithoutCreatedAt_BackwardCompatibility()
    {
        // Simulate old JSON without CreatedAt field
        const string json = """{"JobId":"550e8400-e29b-41d4-a716-446655440000","FileName":"old.pdf","FilePath":"/old.pdf"}""";

        var deserialized = JsonSerializer.Deserialize<OcrCommand>(json);

        deserialized.Should().NotBeNull();
        deserialized!.CreatedAt.Should().BeNull();
        deserialized.FileName.Should().Be("old.pdf");
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
