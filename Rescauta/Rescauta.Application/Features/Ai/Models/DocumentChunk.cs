namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Fragmento de un documento que será convertido en embedding
/// y almacenado en la colección vectorial de Qdrant.
/// </summary>
public sealed record DocumentChunk
{
    /// <summary>Identificador único del fragmento.</summary>
    public Guid Id { get; init; }

    /// <summary>Título del documento de origen.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Contenido textual del fragmento.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Identificador o ruta de la fuente original.
    /// Ejemplo: "como-funciona" o "preguntas-frecuentes".
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Categoría del contenido.
    /// Ejemplo: "donaciones", "comedores" o "seguimiento".
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Metadatos adicionales asociados al fragmento.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}