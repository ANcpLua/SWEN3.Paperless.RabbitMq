namespace SWEN3.Paperless.RabbitMq.Schema;

/// <summary>
///     RabbitMQ topology constants and schema definitions.
///     Defines the exchange, queues, and routing keys used by the Paperless system.
/// </summary>
public static class RabbitMqSchema
{
    /// <summary>
    ///     The main exchange for all Paperless messages.
    ///     Uses topic exchange type for flexible routing.
    /// </summary>
    public const string Exchange = "paperless.exchange";

    /// <summary>
    ///     Queue for OCR processing commands.
    /// </summary>
    public const string OcrCommandQueue = "OcrCommandQueue";

    /// <summary>
    ///     Queue for OCR result events.
    /// </summary>
    public const string OcrEventQueue = "OcrEventQueue";

    /// <summary>
    ///     Routing key for OCR commands.
    /// </summary>
    public const string OcrCommandRouting = "ocr.command";

    /// <summary>
    ///     Routing key for OCR events.
    /// </summary>
    public const string OcrEventRouting = "ocr.event";
}