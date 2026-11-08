
using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Abstracción de la base vectorial Qdrant.
/// La implementación HTTP vive en Infrastructure.
/// </summary>
public interface IQdrantClient
{
    /// <summary>Nombre de la colección configurada.</summary>
    string CollectionName { get; }

    /// <summary>Comprueba la conectividad y configuración de Qdrant.</summary>
    Task<AiConnectivityResult> CheckConnectivityAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Crea la colección si todavía no existe.</summary>
    Task EnsureCollectionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Guarda o actualiza fragmentos junto con sus embeddings.</summary>
    Task UpsertPointsAsync(
        IReadOnlyList<DocumentChunk> documents,
        IReadOnlyList<IReadOnlyList<float>> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>Busca fragmentos similares a un embedding.</summary>
    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        IReadOnlyList<float> embedding,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los Ids de todos los puntos cuyo payload "source" coincide con el valor
    /// indicado. Se usa para conocer la versión almacenada antes de reindexar.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetPointIdsBySourceAsync(
        string source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina los puntos de una fuente. Si <paramref name="keepPointIds"/> tiene
    /// elementos, conserva esos Ids y borra el resto. Con la colección vacía o null,
    /// borra todos los puntos de esa fuente.
    ///
    /// Es la operación de limpieza del reindexado: nunca toca puntos de otras fuentes.
    /// </summary>
    Task DeletePointsBySourceAsync(
        string source,
        IReadOnlyCollection<Guid>? keepPointIds = null,
        CancellationToken cancellationToken = default);
}
