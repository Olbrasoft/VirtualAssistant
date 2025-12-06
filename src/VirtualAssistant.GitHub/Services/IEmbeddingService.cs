using Pgvector;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Interface for generating text embeddings.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The embedding vector, or null if the text is too short or empty.</returns>
    Task<Vector?> GenerateEmbeddingAsync(string? text, CancellationToken ct = default);

    /// <summary>
    /// Generates embeddings for multiple texts in batch.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping text index to embedding vector (null if text was skipped).</returns>
    Task<Dictionary<int, Vector?>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string?> texts,
        CancellationToken ct = default);

    /// <summary>
    /// Gets whether the service is configured and ready to generate embeddings.
    /// </summary>
    bool IsConfigured { get; }
}
