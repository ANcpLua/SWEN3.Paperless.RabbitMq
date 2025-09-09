namespace SWEN3.Paperless.RabbitMq.GenAI;

/// <summary>
///     Defines text summarization capabilities for document content.
/// </summary>
public interface ITextSummarizer
{
    /// <summary>
    ///     Generates a structured summary of the provided text.
    /// </summary>
    /// <param name="text">The text content to summarize. Should not be null or empty.</param>
    /// <param name="cancellationToken">Token to cancel the summarization operation.</param>
    /// <returns>
    ///     A structured summary of the text, or <c>null</c> if summarization failed due to API errors,
    ///     invalid input, or service unavailability.
    /// </returns>
    Task<string?> SummarizeAsync(string text, CancellationToken cancellationToken = default);
}