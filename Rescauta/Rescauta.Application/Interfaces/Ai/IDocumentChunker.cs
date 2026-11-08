using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Convierte un documento en fragmentos coherentes, con contexto y metadatos.
/// La implementación es determinista: el mismo documento produce los mismos Ids.
/// </summary>
public interface IDocumentChunker
{
    IReadOnlyList<DocumentChunk> Chunk(DocumentSource document);
}
