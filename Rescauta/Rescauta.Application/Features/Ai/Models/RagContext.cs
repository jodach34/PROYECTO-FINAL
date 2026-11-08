namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Contexto documental acotado y ya escapado, listo para insertarse en el prompt.
/// <see cref="Text"/> contiene bloques &lt;documento&gt; con el contenido escapado.
/// </summary>
public sealed record RagContext
{
    public string Text { get; init; } = string.Empty;

    public IReadOnlyList<RagContextFragment> Fragments { get; init; }
        = Array.Empty<RagContextFragment>();

    /// <summary>Caracteres de contenido documental seleccionado (sin contar envoltorios).</summary>
    public int CharacterCount { get; init; }
}

/// <summary>
/// Fragmento incluido en el contexto, con su cita estable y procedencia pública.
/// </summary>
public sealed record RagContextFragment
{
    /// <summary>Identificador de cita, por ejemplo "S1".</summary>
    public string CitationId { get; init; } = string.Empty;

    public Guid ChunkId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    /// <summary>Contenido normalizado, sin escapar. Se usa para diagnóstico y pruebas.</summary>
    public string Content { get; init; } = string.Empty;

    public double Score { get; init; }
}
