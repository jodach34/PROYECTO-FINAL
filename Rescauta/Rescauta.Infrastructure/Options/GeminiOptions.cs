namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "Gemini" de appsettings.json.
///
/// La ApiKey es un secreto y NUNCA se guarda en appsettings.json. Se toma de
/// user-secrets en local o de la variable de entorno Gemini__ApiKey en Render.
/// Los modelos y la URL base no son secretos y tienen valores por defecto.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Clave de la API de Google AI Studio. Vacia en appsettings.json a proposito.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo usado para redactar la respuesta del chatbot.</summary>
    public string GenerationModel { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>Modelo usado para generar los vectores del RAG.</summary>
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    /// <summary>Endpoint base de la API. Se puede cambiar sin recompilar.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";

    /// <summary>Timeout de las llamadas HTTP a Gemini, en segundos.</summary>
    public int TimeoutSeconds { get; set; } = 20;
}
