
namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Abstracción del proveedor de generación y embeddings.
/// </summary>
public interface IGeminiClient
{
    Task<AiConnectivityResult> CheckConnectivityAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera texto a partir de una instrucción de sistema y un prompt de usuario.
    /// Los errores se clasifican con <see cref="AiProviderException"/> y nunca
    /// exponen claves ni el cuerpo completo del proveedor.
    /// </summary>
    Task<AiGenerationResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken = default);
}
