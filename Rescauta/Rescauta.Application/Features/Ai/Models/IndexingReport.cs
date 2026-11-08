namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Resultado de una ejecución de indexación. Está pensado para que un administrador
/// sepa exactamente qué se guardó, qué no cambió y qué documento conservó su versión
/// anterior tras un fallo. No incluye contenido ni credenciales.
/// </summary>
public sealed record IndexingReport
{
    /// <summary>Colección de Qdrant usada en la ejecución.</summary>
    public string Collection { get; init; } = string.Empty;

    /// <summary>Cantidad de documentos entregados por el proveedor.</summary>
    public int Documents { get; init; }

    /// <summary>Documentos cuya nueva versión quedó verificada en Qdrant.</summary>
    public int IndexedDocuments { get; init; }

    /// <summary>Documentos cuya versión almacenada ya coincidía con el contenido actual.</summary>
    public int UnchangedDocuments { get; init; }

    /// <summary>Documentos que fallaron y no tenían una versión anterior que conservar.</summary>
    public int FailedDocuments { get; init; }

    /// <summary>Documentos que fallaron pero conservaron íntegra su versión anterior.</summary>
    public int PreservedDocuments { get; init; }

    /// <summary>Documentos indexados correctamente pero con limpieza de puntos obsoletos pendiente.</summary>
    public int PendingCleanupDocuments { get; init; }

    /// <summary>Total de fragmentos preparados.</summary>
    public int Chunks { get; init; }

    /// <summary>Fragmentos cuya escritura se verificó en Qdrant.</summary>
    public int IndexedChunks { get; init; }

    /// <summary>Puntos de versiones anteriores eliminados tras verificar la nueva versión.</summary>
    public int DeletedObsoleteChunks { get; init; }

    /// <summary>Peticiones de upsert realizadas.</summary>
    public int Batches { get; init; }

    /// <summary>Duración total de la indexación, en milisegundos.</summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Error global que impidió preparar la colección. Va vacío cuando la ejecución pudo
    /// procesar los documentos. Nunca expone credenciales.
    /// </summary>
    public string? FatalError { get; init; }

    /// <summary>Detalle por documento, en el mismo orden en que se procesaron.</summary>
    public IReadOnlyList<IndexingDocumentResult> Details { get; init; }
        = Array.Empty<IndexingDocumentResult>();

    /// <summary>True solo si no hubo fallos ni limpiezas pendientes.</summary>
    public bool IsFullySuccessful =>
        FatalError is null &&
        FailedDocuments == 0 &&
        PreservedDocuments == 0 &&
        PendingCleanupDocuments == 0;
}

/// <summary>Resultado de indexar un documento individual.</summary>
public sealed record IndexingDocumentResult
{
    public string Source { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>Uno de los valores de <see cref="IndexingDocumentStatus"/>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Explicación breve y sanitizada de lo ocurrido.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Fragmentos preparados para este documento.</summary>
    public int Chunks { get; init; }

    /// <summary>Fragmentos cuya escritura quedó verificada.</summary>
    public int IndexedChunks { get; init; }

    /// <summary>Puntos obsoletos eliminados.</summary>
    public int DeletedObsoleteChunks { get; init; }

    /// <summary>Peticiones de upsert realizadas para este documento.</summary>
    public int Batches { get; init; }

    /// <summary>Hash del contenido indexado (no es un secreto).</summary>
    public string ContentHash { get; init; } = string.Empty;
}

/// <summary>Estados posibles de un documento dentro del informe.</summary>
public static class IndexingDocumentStatus
{
    /// <summary>La nueva versión se guardó y se verificó.</summary>
    public const string Indexed = "indexado";

    /// <summary>El contenido actual ya estaba almacenado; no se tocaron puntos.</summary>
    public const string Unchanged = "sin_cambios";

    /// <summary>Falló la indexación y se conservó la versión anterior.</summary>
    public const string PreservedPreviousVersion = "conservo_version_anterior";

    /// <summary>La nueva versión está verificada, pero quedaron puntos obsoletos por limpiar.</summary>
    public const string IndexedWithPendingCleanup = "indexado_con_pendientes";

    /// <summary>Falló la indexación y no existía una versión anterior.</summary>
    public const string Failed = "fallido";
}
