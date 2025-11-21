namespace SWEN3.Paperless.RabbitMq.Models;

/// <summary>
///     Command to initiate GenAI summarization after successful OCR.
///     Published by REST service after persisting OCR content.
/// </summary>
/// <param name="DocumentId">The document ID to summarize.</param>
/// <param name="Text">The OCR-extracted text to summarize.</param>
public record GenAICommand(Guid DocumentId, string Text);
