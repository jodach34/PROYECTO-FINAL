namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Documento institucional curado que se va a fragmentar e indexar.
///
/// No representa una entidad de base de datos: es contenido público que vive en el
/// repositorio (Markdown embebido) y que el proveedor de documentos entrega en memoria.
/// </summary>
public sealed record DocumentSource
{
    /// <summary>Identificador estable del documento, por ejemplo "donaciones".</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Título legible que se mostrará junto al fragmento recuperado.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Categoría temática, por ejemplo "donaciones" o "privacidad".</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Contenido completo en Markdown.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Metadatos públicos adicionales que se copiarán a cada fragmento.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}
