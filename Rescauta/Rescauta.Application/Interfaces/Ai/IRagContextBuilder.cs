using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Convierte los fragmentos recuperados en un contexto acotado, deduplicado y con
/// procedencia. El contexto no contiene instrucciones ejecutables.
/// </summary>
public interface IRagContextBuilder
{
    RagContext Build(IReadOnlyList<DocumentSearchResult> fragments);

    RagContext Empty { get; }
}
