namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Configuración de la preparación, fragmentación e indexación documental.
/// Se enlaza a la sección "AiIndexing" de appsettings.json. No contiene secretos.
/// </summary>
public sealed class AiIndexingOptions
{
    public const string SectionName = "AiIndexing";

    /// <summary>Tamaño máximo de cada fragmento, incluyendo el contexto de título y sección.</summary>
    public int ChunkMaxCharacters { get; set; } = 1200;

    /// <summary>Caracteres de solapamiento que se repiten entre fragmentos consecutivos.</summary>
    public int ChunkOverlapCharacters { get; set; } = 200;

    /// <summary>Longitud mínima de contenido para conservar un fragmento.</summary>
    public int ChunkMinCharacters { get; set; } = 40;

    /// <summary>Cantidad de fragmentos por petición de upsert a Qdrant.</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>Llamadas simultáneas máximas a Gemini para generar embeddings.</summary>
    public int MaxConcurrentEmbeddings { get; set; } = 3;

    /// <summary>Reintentos ante un fallo transitorio de Gemini.</summary>
    public int EmbeddingRetryAttempts { get; set; } = 2;

    /// <summary>Espera entre reintentos de embeddings, en milisegundos.</summary>
    public int EmbeddingRetryDelayMilliseconds { get; set; } = 750;

    /// <summary>Reintentos de verificación y limpieza de puntos obsoletos en Qdrant.</summary>
    public int CleanupRetryAttempts { get; set; } = 2;

    /// <summary>Espera entre reintentos de limpieza, en milisegundos.</summary>
    public int CleanupRetryDelayMilliseconds { get; set; } = 500;
}
