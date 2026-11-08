using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Indexa documentación institucional con una estrategia de reemplazo segura.
///
/// Garantías:
///   1. Antes de tocar Qdrant se preparan TODOS los fragmentos y TODOS sus embeddings.
///      Si Gemini falla, no se modifica ningún punto.
///   2. La versión nueva usa Ids derivados del contenido. Mientras no se verifiquen sus
///      Ids en Qdrant, la versión anterior no se borra.
///   3. Si falla un lote de escritura, se eliminan solo los puntos nuevos que no
///      pertenecían a la versión anterior. La versión anterior queda intacta.
///   4. Solo después de verificar que la nueva versión está completa se eliminan los
///      puntos obsoletos del mismo documento. Si esa limpieza falla, se informa como
///      "indexado_con_pendientes" en vez de declarar éxito total.
///
/// La indexación nunca se ejecuta al arrancar la API: la dispara un administrador.
/// </summary>
public sealed class DocumentIndexingService(
    IDocumentSourceProvider documentSourceProvider,
    IDocumentChunker documentChunker,
    IGeminiClient geminiClient,
    IQdrantClient qdrantClient,
    IOptions<AiIndexingOptions> options,
    ILogger<DocumentIndexingService> logger) : IDocumentIndexer
{
    // Evita que dos peticiones administrativas reindexen a la vez y compitan por los
    // mismos puntos. Es estático porque el servicio es scoped.
    private static readonly SemaphoreSlim IndexingGate = new(1, 1);

    public async Task<IndexingReport> IndexAsync(CancellationToken cancellationToken = default)
    {
        await IndexingGate.WaitAsync(cancellationToken);

        try
        {
            return await IndexCoreAsync(cancellationToken);
        }
        finally
        {
            IndexingGate.Release();
        }
    }

    private async Task<IndexingReport> IndexCoreAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var batchSize = Math.Max(1, settings.BatchSize);
        var concurrency = Math.Max(1, settings.MaxConcurrentEmbeddings);
        var embeddingRetries = Math.Max(0, settings.EmbeddingRetryAttempts);
        var embeddingRetryDelay = Math.Max(0, settings.EmbeddingRetryDelayMilliseconds);
        var cleanupRetries = Math.Max(0, settings.CleanupRetryAttempts);
        var cleanupRetryDelay = Math.Max(0, settings.CleanupRetryDelayMilliseconds);

        var collection = qdrantClient.CollectionName;
        var started = Stopwatch.StartNew();
        IReadOnlyList<DocumentSource> documents;

        try
        {
            documents = documentSourceProvider.GetDocuments();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer los documentos institucionales a indexar.");

            return new IndexingReport
            {
                Collection = collection,
                DurationMs = started.ElapsedMilliseconds,
                FatalError =
                    "No se pudieron leer los documentos institucionales. Revisa los recursos embebidos de la API."
            };
        }

        try
        {
            await qdrantClient.EnsureCollectionAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "No se pudo preparar la colección {Collection}.", collection);

            return new IndexingReport
            {
                Collection = collection,
                Documents = documents.Count,
                FailedDocuments = documents.Count,
                DurationMs = started.ElapsedMilliseconds,
                FatalError =
                    "No se pudo preparar la colección de Qdrant. Revisa la configuración y los logs de la API."
            };
        }

        var details = new List<IndexingDocumentResult>(documents.Count);
        var totalChunks = 0;
        var indexedChunks = 0;
        var deletedObsolete = 0;
        var batches = 0;

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IndexingDocumentResult result;

            try
            {
                result = await IndexDocumentAsync(
                    document,
                    batchSize,
                    concurrency,
                    embeddingRetries,
                    embeddingRetryDelay,
                    cleanupRetries,
                    cleanupRetryDelay,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error inesperado indexando el documento {Source}.",
                    document.Source);

                result = new IndexingDocumentResult
                {
                    Source = document.Source,
                    Title = document.Title,
                    Status = IndexingDocumentStatus.Failed,
                    Message =
                        "Error inesperado durante la indexación. No se modificó la versión anterior."
                };
            }

            details.Add(result);
            totalChunks += result.Chunks;
            indexedChunks += result.IndexedChunks;
            deletedObsolete += result.DeletedObsoleteChunks;
            batches += result.Batches;
        }

        var report = new IndexingReport
        {
            Collection = collection,
            Documents = documents.Count,
            IndexedDocuments = details.Count(item =>
                item.Status is IndexingDocumentStatus.Indexed
                    or IndexingDocumentStatus.IndexedWithPendingCleanup),
            UnchangedDocuments = details.Count(item =>
                item.Status == IndexingDocumentStatus.Unchanged),
            FailedDocuments = details.Count(item =>
                item.Status == IndexingDocumentStatus.Failed),
            PreservedDocuments = details.Count(item =>
                item.Status == IndexingDocumentStatus.PreservedPreviousVersion),
            PendingCleanupDocuments = details.Count(item =>
                item.Status == IndexingDocumentStatus.IndexedWithPendingCleanup),
            Chunks = totalChunks,
            IndexedChunks = indexedChunks,
            DeletedObsoleteChunks = deletedObsolete,
            Batches = batches,
            DurationMs = started.ElapsedMilliseconds,
            Details = details
        };

        logger.LogInformation(
            "Indexación finalizada en {DurationMs} ms: {Documents} documentos, " +
            "{Indexed} indexados, {Unchanged} sin cambios, {Preserved} con versión anterior conservada, " +
            "{Failed} fallidos, {Pending} con limpieza pendiente, {Chunks} fragmentos.",
            report.DurationMs,
            report.Documents,
            report.IndexedDocuments,
            report.UnchangedDocuments,
            report.PreservedDocuments,
            report.FailedDocuments,
            report.PendingCleanupDocuments,
            report.IndexedChunks);

        return report;
    }

    private async Task<IndexingDocumentResult> IndexDocumentAsync(
        DocumentSource document,
        int batchSize,
        int concurrency,
        int embeddingRetries,
        int embeddingRetryDelay,
        int cleanupRetries,
        int cleanupRetryDelay,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentChunk> chunks;

        try
        {
            chunks = documentChunker.Chunk(document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo fragmentar el documento {Source}.", document.Source);

            return Result(
                document,
                IndexingDocumentStatus.Failed,
                "No se pudo fragmentar el documento. No se modificó Qdrant.",
                chunks: 0);
        }

        if (chunks.Count == 0)
        {
            return Result(
                document,
                IndexingDocumentStatus.Failed,
                "El documento no produjo fragmentos indexables. No se modificó Qdrant.",
                chunks: 0);
        }

        var contentHash = chunks[0].Metadata.GetValueOrDefault("contentHash", string.Empty);
        var newIds = chunks.Select(chunk => chunk.Id).ToHashSet();
        IReadOnlyList<Guid> storedBefore;

        try
        {
            storedBefore = await qdrantClient.GetPointIdsBySourceAsync(
                document.Source,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "No se pudo leer la versión actual del documento {Source}; no se modificará.",
                document.Source);

            return Result(
                document,
                IndexingDocumentStatus.Failed,
                "No se pudo leer la versión actual en Qdrant. Para evitar pérdidas, no se escribió nada.",
                chunks.Count,
                contentHash: contentHash);
        }

        var oldIds = storedBefore.ToHashSet();

        if (oldIds.Count > 0 && oldIds.SetEquals(newIds))
        {
            return Result(
                document,
                IndexingDocumentStatus.Unchanged,
                "La versión almacenada ya coincide con el contenido actual.",
                chunks.Count,
                contentHash: contentHash);
        }

        IReadOnlyList<IReadOnlyList<float>> embeddings;

        try
        {
            embeddings = await GenerateEmbeddingsAsync(
                chunks,
                concurrency,
                embeddingRetries,
                embeddingRetryDelay,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Falló la generación de embeddings del documento {Source}.",
                document.Source);

            return Result(
                document,
                oldIds.Count > 0
                    ? IndexingDocumentStatus.PreservedPreviousVersion
                    : IndexingDocumentStatus.Failed,
                oldIds.Count > 0
                    ? "Falló la generación de embeddings. Se conserva la versión anterior sin cambios."
                    : "Falló la generación de embeddings. No había una versión anterior que conservar.",
                chunks.Count,
                contentHash: contentHash);
        }

        if (embeddings.Count != chunks.Count)
        {
            return Result(
                document,
                oldIds.Count > 0
                    ? IndexingDocumentStatus.PreservedPreviousVersion
                    : IndexingDocumentStatus.Failed,
                "La cantidad de embeddings no coincide con la de fragmentos. No se modificó Qdrant.",
                chunks.Count,
                contentHash: contentHash);
        }

        var batches = 0;

        try
        {
            for (var offset = 0; offset < chunks.Count; offset += batchSize)
            {
                var batchDocuments = chunks.Skip(offset).Take(batchSize).ToList();
                var batchEmbeddings = embeddings.Skip(offset).Take(batchSize).ToList();

                await qdrantClient.UpsertPointsAsync(
                    batchDocuments,
                    batchEmbeddings,
                    cancellationToken);

                batches++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Falló la escritura de {Source} en Qdrant tras {Batches} lote(s).",
                document.Source,
                batches);

            var rollback = await RemoveNewVersionAsync(
                document.Source,
                oldIds,
                cleanupRetries,
                cleanupRetryDelay,
                cancellationToken);

            var preserved = oldIds.Count > 0;

            return Result(
                document,
                preserved
                    ? IndexingDocumentStatus.PreservedPreviousVersion
                    : IndexingDocumentStatus.Failed,
                preserved
                    ? rollback
                        ? "Falló la escritura en Qdrant. La versión anterior se conservó y los puntos nuevos parciales se limpiaron."
                        : "Falló la escritura en Qdrant. La versión anterior se conservó; no se pudo confirmar la limpieza de los puntos nuevos parciales."
                    : rollback
                        ? "Falló la escritura en Qdrant y no había versión anterior. Se limpiaron los puntos nuevos parciales."
                        : "Falló la escritura en Qdrant y no había versión anterior. No se pudo confirmar la limpieza de los puntos nuevos parciales.",
                chunks.Count,
                indexedChunks: 0,
                batches: batches,
                contentHash: contentHash);
        }

        IReadOnlyList<Guid> storedAfterUpsert;

        try
        {
            storedAfterUpsert = await qdrantClient.GetPointIdsBySourceAsync(
                document.Source,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "No se pudo verificar la escritura del documento {Source}.",
                document.Source);

            var rollback = await RemoveNewVersionAsync(
                document.Source,
                oldIds,
                cleanupRetries,
                cleanupRetryDelay,
                cancellationToken);

            var preserved = oldIds.Count > 0;

            return Result(
                document,
                preserved
                    ? IndexingDocumentStatus.PreservedPreviousVersion
                    : IndexingDocumentStatus.Failed,
                preserved
                    ? rollback
                        ? "Qdrant no confirmó la verificación. Se revirtió la versión nueva y se conservó la anterior."
                        : "Qdrant no confirmó la verificación. La versión anterior se conservó; la limpieza de la versión nueva quedó pendiente."
                    : rollback
                        ? "Qdrant no confirmó la verificación y no había versión anterior. Se limpiaron los puntos nuevos."
                        : "Qdrant no confirmó la verificación y no había versión anterior. La limpieza quedó pendiente.",
                chunks.Count,
                indexedChunks: 0,
                batches: batches,
                contentHash: contentHash);
        }

        var storedSet = storedAfterUpsert.ToHashSet();
        var missingNewIds = newIds.Except(storedSet).ToList();

        if (missingNewIds.Count > 0)
        {
            logger.LogError(
                "Qdrant no almacenó {Missing} de {Total} fragmentos nuevos del documento {Source}.",
                missingNewIds.Count,
                newIds.Count,
                document.Source);

            var rollback = await RemoveNewVersionAsync(
                document.Source,
                oldIds,
                cleanupRetries,
                cleanupRetryDelay,
                cancellationToken);

            var preserved = oldIds.Count > 0;

            return Result(
                document,
                preserved
                    ? IndexingDocumentStatus.PreservedPreviousVersion
                    : IndexingDocumentStatus.Failed,
                preserved
                    ? rollback
                        ? $"Qdrant no confirmó {missingNewIds.Count} fragmento(s) nuevos. Se revirtió la versión nueva y se conservó la anterior."
                        : $"Qdrant no confirmó {missingNewIds.Count} fragmento(s) nuevos. La versión anterior se conservó; la limpieza quedó pendiente."
                    : rollback
                        ? $"Qdrant no confirmó {missingNewIds.Count} fragmento(s) nuevos. Se limpiaron los puntos nuevos parciales."
                        : $"Qdrant no confirmó {missingNewIds.Count} fragmento(s) nuevos. La limpieza quedó pendiente.",
                chunks.Count,
                indexedChunks: storedSet.Count(id => newIds.Contains(id)),
                batches: batches,
                contentHash: contentHash);
        }

        var obsoleteIds = oldIds.Except(newIds).ToList();

        if (obsoleteIds.Count == 0)
        {
            return Result(
                document,
                IndexingDocumentStatus.Indexed,
                "Nueva versión verificada en Qdrant.",
                chunks.Count,
                indexedChunks: chunks.Count,
                batches: batches,
                contentHash: contentHash);
        }

        var cleanup = await CleanupObsoleteAsync(
            document.Source,
            newIds,
            obsoleteIds.Count,
            cleanupRetries,
            cleanupRetryDelay,
            cancellationToken);

        if (!cleanup.Completed)
        {
            logger.LogWarning(
                "El documento {Source} se indexó, pero quedaron {Remaining} punto(s) obsoletos.",
                document.Source,
                cleanup.Remaining);

            return Result(
                document,
                IndexingDocumentStatus.IndexedWithPendingCleanup,
                $"Nueva versión verificada, pero quedaron {cleanup.Remaining} punto(s) obsoletos por limpiar.",
                chunks.Count,
                indexedChunks: chunks.Count,
                deletedObsoleteChunks: cleanup.Deleted,
                batches: batches,
                contentHash: contentHash);
        }

        return Result(
            document,
            IndexingDocumentStatus.Indexed,
            "Nueva versión verificada y versión anterior eliminada.",
            chunks.Count,
            indexedChunks: chunks.Count,
            deletedObsoleteChunks: cleanup.Deleted,
            batches: batches,
            contentHash: contentHash);
    }

    private async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(
        IReadOnlyList<DocumentChunk> chunks,
        int concurrency,
        int retries,
        int retryDelayMilliseconds,
        CancellationToken cancellationToken)
    {
        var results = new IReadOnlyList<float>[chunks.Count];
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        var tasks = chunks.Select(async (chunk, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                results[index] = await GenerateEmbeddingWithRetryAsync(
                    chunk.Content,
                    retries,
                    retryDelayMilliseconds,
                    cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return results;
    }

    private async Task<IReadOnlyList<float>> GenerateEmbeddingWithRetryAsync(
        string text,
        int retries,
        int retryDelayMilliseconds,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                return await geminiClient.GenerateEmbeddingAsync(text, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (attempt < retries && retryDelayMilliseconds > 0)
                {
                    await Task.Delay(retryDelayMilliseconds, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            "No se pudo generar un embedding tras los reintentos configurados.",
            lastError);
    }

    /// <summary>
    /// Revierte la versión nueva conservando únicamente los Ids de la versión anterior.
    /// Si no había versión anterior, elimina todos los puntos de esa fuente.
    /// </summary>
    private async Task<bool> RemoveNewVersionAsync(
        string source,
        IReadOnlySet<Guid> oldIds,
        int retries,
        int retryDelayMilliseconds,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                await qdrantClient.DeletePointsBySourceAsync(
                    source,
                    oldIds,
                    cancellationToken);

                var remaining = await qdrantClient.GetPointIdsBySourceAsync(
                    source,
                    cancellationToken);

                if (oldIds.All(id => remaining.Contains(id)))
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "No se pudo revertir la versión nueva del documento {Source} (intento {Attempt}).",
                    source,
                    attempt + 1);
            }

            if (attempt < retries && retryDelayMilliseconds > 0)
            {
                await Task.Delay(retryDelayMilliseconds, cancellationToken);
            }
        }

        return false;
    }

    private async Task<CleanupResult> CleanupObsoleteAsync(
        string source,
        IReadOnlySet<Guid> newIds,
        int obsoleteCount,
        int retries,
        int retryDelayMilliseconds,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                await qdrantClient.DeletePointsBySourceAsync(
                    source,
                    newIds,
                    cancellationToken);

                var remaining = await qdrantClient.GetPointIdsBySourceAsync(
                    source,
                    cancellationToken);

                var extras = remaining.Count(id => !newIds.Contains(id));

                if (extras == 0)
                {
                    return new CleanupResult(
                        Completed: true,
                        Deleted: obsoleteCount,
                        Remaining: 0);
                }

                if (attempt == retries)
                {
                    return new CleanupResult(
                        Completed: false,
                        Deleted: Math.Max(0, obsoleteCount - extras),
                        Remaining: extras);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "No se pudieron limpiar los puntos obsoletos del documento {Source} (intento {Attempt}).",
                    source,
                    attempt + 1);
            }

            if (retryDelayMilliseconds > 0)
            {
                await Task.Delay(retryDelayMilliseconds, cancellationToken);
            }
        }

        return new CleanupResult(
            Completed: false,
            Deleted: 0,
            Remaining: obsoleteCount);
    }

    private static IndexingDocumentResult Result(
        DocumentSource document,
        string status,
        string message,
        int chunks,
        int indexedChunks = 0,
        int deletedObsoleteChunks = 0,
        int batches = 0,
        string contentHash = "")
        => new()
        {
            Source = document.Source,
            Title = document.Title,
            Status = status,
            Message = message,
            Chunks = chunks,
            IndexedChunks = indexedChunks,
            DeletedObsoleteChunks = deletedObsoleteChunks,
            Batches = batches,
            ContentHash = contentHash
        };

    private readonly record struct CleanupResult(bool Completed, int Deleted, int Remaining);
}
