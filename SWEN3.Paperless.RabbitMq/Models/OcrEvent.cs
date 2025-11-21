using SWEN3.Paperless.RabbitMq.Publishing;

namespace SWEN3.Paperless.RabbitMq.Models;

/// <summary>
///     Represents an OCR processing result event.
///     This event is published after processing an <see cref="OcrCommand" />.
/// </summary>
/// <param name="JobId">Unique identifier for the OCR job matching the <see cref="OcrCommand.JobId" />.</param>
/// <param name="Status">Processing status (Completed/Failed).</param>
/// <param name="Text">Extracted text content (null if failed).</param>
/// <param name="ProcessedAt">Timestamp when processing completed.</param>
/// <seealso cref="OcrCommand" />
/// <seealso cref="PublishingExtensions.PublishOcrEventAsync{T}" />
public record OcrEvent(Guid JobId, string Status, string? Text, DateTimeOffset ProcessedAt);
