using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Recuperación semántica de solo lectura:
///   1. genera el embedding de la pregunta con el mismo método usado al indexar,
///   2. busca en la colección configurada,
///   3. filtra por relevancia y ordena de forma determinista.
///
/// No escribe, no crea colecciones y no reindexa nada.
/// </summary>
public sealed class RagRetriever(
    IGeminiClient geminiClient,
    IQdrantClient qdrantClient,
    IOptions<AiRagOptions> options,
    ILogger<RagRetriever> logger) : IRagRetriever
{
    public async Task<IReadOnlyList<DocumentSearchResult>> RetrieveAsync(
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
        {
            throw new ArgumentException(
                "La pregunta no puede estar vacía.",
                nameof(pregunta));
        }

        var settings = options.Value;
        var candidateLimit = Math.Clamp(settings.CandidateLimit, 1, 50);
        var minScore = Math.Clamp(settings.MinRelevanceScore, -1d, 1d);

        IReadOnlyList<float> embedding;

        try
        {
            using var timeout = CreateTimeout(
                settings.EmbeddingTimeoutSeconds,
                cancellationToken);

            embedding = await geminiClient.GenerateEmbeddingAsync(
                pregunta,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("La generación del embedding excedió el timeout configurado.");

            throw new RagRetrievalException(
                RagRetrievalFailure.AiTimeout,
                "Gemini no respondió dentro del tiempo configurado.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("No se pudo generar el embedding de la pregunta: {Error}", ex.HttpRequestError);

            throw new RagRetrievalException(
                RagRetrievalFailure.AiUnavailable,
                "No se pudo conectar con Gemini.",
                ex);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Gemini no está configurado correctamente para generar embeddings.");

            throw new RagRetrievalException(
                RagRetrievalFailure.AiNotConfigured,
                "La configuración de Gemini no es válida.",
                ex);
        }
        catch (ArgumentException ex)
        {
            logger.LogError("Gemini devolvió un embedding con formato inválido.");

            throw new RagRetrievalException(
                RagRetrievalFailure.AiInvalidResponse,
                "Gemini devolvió un embedding inválido.",
                ex);
        }

        IReadOnlyList<DocumentSearchResult> results;

        try
        {
            using var timeout = CreateTimeout(
                settings.QdrantTimeoutSeconds,
                cancellationToken);

            results = await qdrantClient.SearchAsync(
                embedding,
                candidateLimit,
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("La búsqueda en Qdrant excedió el timeout configurado.");

            throw new RagRetrievalException(
                RagRetrievalFailure.QdrantTimeout,
                "Qdrant no respondió dentro del tiempo configurado.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("No se pudo consultar Qdrant: {Error}", ex.HttpRequestError);

            throw new RagRetrievalException(
                RagRetrievalFailure.QdrantUnavailable,
                "No se pudo conectar con Qdrant.",
                ex);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError("Qdrant devolvió una respuesta inválida durante la búsqueda.");

            throw new RagRetrievalException(
                RagRetrievalFailure.QdrantUnavailable,
                "Qdrant devolvió una respuesta inválida.",
                ex);
        }

        var filtered = results
            .Where(result => !string.IsNullOrWhiteSpace(result.Content))
            .Where(result => result.Score >= minScore)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Source, StringComparer.Ordinal)
            .ThenBy(result => result.Id)
            .ToArray();

        logger.LogInformation(
            "Recuperación finalizada: {Candidates} candidatos, {Selected} sobre el umbral, " +
            "mejor puntuación {TopScore:F3}.",
            results.Count,
            filtered.Length,
            filtered.Length > 0 ? filtered[0].Score : 0d);

        return filtered;
    }

    private static CancellationTokenSource CreateTimeout(
        int seconds,
        CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, seconds)));
        return timeout;
    }
}
