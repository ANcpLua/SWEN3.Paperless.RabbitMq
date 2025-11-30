using SWEN3.Paperless.RabbitMq.Publishing;

namespace SWEN3.Paperless.RabbitMq.Models;

/// <summary>
///     Represents an OCR processing command.
///     Use <see cref="PublishingExtensions.PublishOcrCommandAsync{T}" /> to publish this command.
/// </summary>
/// <param name="JobId">Unique identifier for the OCR job.</param>
/// <param name="FileName">Name of the file to process.</param>
/// <param name="FilePath">Path to the file in storage.</param>
/// <param name="CreatedAt">
///     When the document was originally uploaded. Optional for backward compatibility.
///     Consumers should fall back to <see cref="DateTimeOffset.UtcNow" /> if null.
/// </param>
/// <seealso cref="OcrEvent" />
public record OcrCommand(Guid JobId, string FileName, string FilePath, DateTimeOffset? CreatedAt = null);
