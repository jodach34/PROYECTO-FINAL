namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Configuración del motor RAG. Se enlaza a la sección "AiRag" de appsettings.json.
/// No contiene secretos: las claves siguen en user-secrets o variables de entorno.
/// </summary>
public sealed class AiRagOptions
{
    public const string SectionName = "AiRag";

    /// <summary>Longitud mínima de la pregunta, sin contar espacios extremos.</summary>
    public int MinQuestionLength { get; set; } = 3;

    /// <summary>Longitud máxima de la pregunta.</summary>
    public int MaxQuestionLength { get; set; } = 500;

    /// <summary>Cantidad de fragmentos solicitados a Qdrant antes de filtrar.</summary>
    public int CandidateLimit { get; set; } = 12;

    /// <summary>Cantidad máxima de fragmentos incluidos en el contexto.</summary>
    public int MaxFragments { get; set; } = 5;

    /// <summary>Cantidad máxima de fragmentos por documento de origen.</summary>
    public int MaxFragmentsPerSource { get; set; } = 3;

    /// <summary>Puntuación mínima de similitud para considerar relevante un fragmento.</summary>
    public double MinRelevanceScore { get; set; } = 0.35;

    /// <summary>
    /// Umbral de Jaccard de trigramas para considerar dos fragmentos casi duplicados.
    /// Debe ser alto para no eliminar fragmentos complementarios.
    /// </summary>
    public double NearDuplicateThreshold { get; set; } = 0.95;

    /// <summary>Caracteres máximos que se conservan de cada fragmento.</summary>
    public int MaxFragmentCharacters { get; set; } = 1600;

    /// <summary>Caracteres máximos de contenido documental incluido en el contexto.</summary>
    public int MaxContextCharacters { get; set; } = 6000;

    /// <summary>Caracteres máximos de la respuesta final.</summary>
    public int MaxAnswerCharacters { get; set; } = 2000;

    /// <summary>Temperatura de generación. Baja para privilegiar respuestas fieles.</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Tokens máximos de salida solicitados a Gemini.</summary>
    public int MaxOutputTokens { get; set; } = 700;

    /// <summary>Timeout propio para generar el embedding de la pregunta.</summary>
    public int EmbeddingTimeoutSeconds { get; set; } = 10;

    /// <summary>Timeout propio para la búsqueda en Qdrant.</summary>
    public int QdrantTimeoutSeconds { get; set; } = 10;

    /// <summary>Timeout propio para la generación de la respuesta.</summary>
    public int GenerationTimeoutSeconds { get; set; } = 30;

    /// <summary>Generaciones simultáneas máximas para proteger la cuota de Gemini.</summary>
    public int MaxConcurrentGenerations { get; set; } = 4;

    /// <summary>Espera máxima para obtener un turno de generación antes de devolver 503.</summary>
    public int ConcurrencyWaitSeconds { get; set; } = 2;

    /// <summary>Exige al menos una cita [S#] válida en respuestas con contexto.</summary>
    public bool RequireCitations { get; set; } = true;
}
