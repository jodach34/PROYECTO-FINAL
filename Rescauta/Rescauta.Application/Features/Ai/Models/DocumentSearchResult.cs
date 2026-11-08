namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Resultado de una búsqueda semántica en Qdrant.
/// Contiene el fragmento recuperado y su puntuación de relevancia.
/// </summary>
public sealed record DocumentSearchResult
{
    /// <summary>Identificador del fragmento almacenado en Qdrant.</summary>
    public Guid Id { get; init; }

    /// <summary>Título del documento original.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Contenido textual recuperado.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Fuente original del contenido.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Categoría del contenido.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Puntuación de similitud devuelta por Qdrant.
    /// Cuanto mayor sea, mayor será la similitud con la consulta.
    /// </summary>
    public double Score { get; init; }

    /// <summary>Metadatos adicionales del fragmento.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}