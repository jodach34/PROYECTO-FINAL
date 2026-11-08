using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Orquesta la preparación, generación de embeddings y almacenamiento seguro en Qdrant.
/// </summary>
public interface IDocumentIndexer
{
    Task<IndexingReport> IndexAsync(CancellationToken cancellationToken = default);
}
