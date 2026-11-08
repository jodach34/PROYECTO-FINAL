using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Recupera fragmentos documentales relevantes para una pregunta.
/// Es estrictamente de solo lectura sobre la colección vectorial.
/// </summary>
public interface IRagRetriever
{
    Task<IReadOnlyList<DocumentSearchResult>> RetrieveAsync(
        string pregunta,
        CancellationToken cancellationToken = default);
}
