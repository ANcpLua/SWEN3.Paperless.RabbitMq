using SWEN3.Paperless.RabbitMq.Publishing;

namespace SWEN3.Paperless.RabbitMq.Models;

/// <summary>
///     Represents a GenAI processing result event.
///     This event is published after processing a <see cref="GenAICommand" />.
/// </summary>
/// <param name="DocumentId">Unique identifier for the document being summarized, matching the <see cref="GenAICommand.DocumentId" />.</param>
/// <param name="Summary">The generated summary text (non-null on success).</param>
/// <param name="GeneratedAt">Timestamp when the summary was generated.</param>
/// <param name="ErrorMessage">Optional error message if processing failed.</param>
/// <seealso cref="GenAICommand" />
/// <seealso cref="GenAIPublishingExtensions.PublishGenAIEventAsync{T}" />
public record GenAIEvent(Guid DocumentId, string? Summary, DateTimeOffset GeneratedAt, string? ErrorMessage = null);
