using Application.Services;

namespace Infrastructure.Services;

/// <summary>
/// Placeholder until the embeddings backend is wired up (docker-compose serves
/// nomic-embed-text-v1.5 over HTTP on :8001; see <c>OllamaSettings.OllamaBaseUrl</c>).
/// Registered so the DI graph is complete — search requests fail with a clear message
/// instead of the whole app failing to resolve.
/// </summary>
public sealed class EmbeddingsService : IEmbeddingsService
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            "Query embedding generation is not implemented yet; wire this service to the embeddings container (README §5).");
}
