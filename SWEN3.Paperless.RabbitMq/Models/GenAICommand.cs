using SWEN3.Paperless.RabbitMq.Publishing;

namespace SWEN3.Paperless.RabbitMq.Models;

/// <summary>
///     Command to initiate GenAI summarization after successful OCR.
///     Published by REST service after persisting OCR content.
///     Use <see cref="GenAIPublishingExtensions.PublishGenAICommandAsync{T}" /> to publish this command.
/// </summary>
/// <param name="DocumentId">The document ID to summarize.</param>
/// <param name="Text">The OCR-extracted text to summarize.</param>
/// <seealso cref="GenAIEvent" />
public record GenAICommand(Guid DocumentId, string Text);
