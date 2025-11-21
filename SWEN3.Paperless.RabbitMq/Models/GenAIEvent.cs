using SWEN3.Paperless.RabbitMq.Publishing;

namespace SWEN3.Paperless.RabbitMq.Models;

/// <summary>
///     Represents a GenAI processing result event.
///     This event is published after AI-based text summarization.
///     Success is indicated by a non-null Summary; failure by a non-null ErrorMessage.
/// </summary>
/// <param name="DocumentId">Unique identifier for the document being summarized.</param>
/// <param name="Summary">The generated summary text (non-null on success).</param>
/// <param name="GeneratedAt">Timestamp when the summary was generated.</param>
/// <param name="ErrorMessage">Optional error message if processing failed.</param>
/// <seealso cref="OcrEvent" />
/// <seealso cref="GenAIPublishingExtensions.PublishGenAIEventAsync{T}" />
public record GenAIEvent(Guid DocumentId, string? Summary, DateTimeOffset GeneratedAt, string? ErrorMessage = null);
