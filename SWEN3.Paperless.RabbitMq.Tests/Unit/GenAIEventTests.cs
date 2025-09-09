namespace SWEN3.Paperless.RabbitMq.Tests.Unit;

public class GenAIEventTests
{
    private const string TestSummaryFull = "This is a test summary";
    private const string TestSummaryShort = "Test summary";

    [Fact]
    public void GenAIEvent_ShouldCreateWithAllProperties()
    {
        var documentId = Guid.NewGuid();
        var generatedAt = DateTimeOffset.UtcNow;
        const string errorMessage = "Test error";
        var genAiEvent = new GenAIEvent(documentId, TestSummaryFull, generatedAt, errorMessage);
        genAiEvent.DocumentId.Should().Be(documentId);
        genAiEvent.Summary.Should().Be(TestSummaryFull);
        genAiEvent.GeneratedAt.Should().Be(generatedAt);
        genAiEvent.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void GenAIEvent_ShouldCreateWithoutErrorMessage()
    {
        var documentId = Guid.NewGuid();
        var generatedAt = DateTimeOffset.UtcNow;
        var genAiEvent = new GenAIEvent(documentId, TestSummaryFull, generatedAt);
        genAiEvent.DocumentId.Should().Be(documentId);
        genAiEvent.Summary.Should().Be(TestSummaryFull);
        genAiEvent.GeneratedAt.Should().Be(generatedAt);
        genAiEvent.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void GenAIEvent_Records_ShouldBeEqualWhenPropertiesAreEqual()
    {
        var documentId = Guid.NewGuid();
        var generatedAt = DateTimeOffset.UtcNow;
        var event1 = new GenAIEvent(documentId, TestSummaryShort, generatedAt);
        var event2 = new GenAIEvent(documentId, TestSummaryShort, generatedAt);
        event1.Should().Be(event2);
        event1.GetHashCode().Should().Be(event2.GetHashCode());
    }

    [Fact]
    public void GenAIEvent_Records_ShouldNotBeEqualWhenPropertiesDiffer()
    {
        var documentId = Guid.NewGuid();
        var generatedAt = DateTimeOffset.UtcNow;
        var event1 = new GenAIEvent(documentId, "Summary 1", generatedAt);
        var event2 = new GenAIEvent(documentId, "Summary 2", generatedAt);
        event1.Should().NotBe(event2);
    }

    [Fact]
    public void GenAIEvent_WithOperator_ShouldCreateModifiedCopy()
    {
        var original = new GenAIEvent(Guid.NewGuid(), "Original summary", DateTimeOffset.UtcNow);
        var modified = original with { Summary = "Modified summary" };
        modified.DocumentId.Should().Be(original.DocumentId);
        modified.Summary.Should().Be("Modified summary");
        modified.GeneratedAt.Should().Be(original.GeneratedAt);
        modified.ErrorMessage.Should().Be(original.ErrorMessage);
    }
}