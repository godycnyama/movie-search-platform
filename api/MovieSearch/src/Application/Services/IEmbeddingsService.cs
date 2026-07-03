namespace Application.Services;

/// <summary>
/// Generates query embeddings in the same 768-dimensional vector space the pipeline
/// used to embed the catalogue (README §6), so cosine similarity is meaningful.
/// </summary>
public interface IEmbeddingsService
{
    /// <summary>Embeds a natural-language query; the result feeds pgvector similarity search.</summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
