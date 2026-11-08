namespace Rescauta.Application.Features.Ai.Dto;

/// <summary>Códigos de error estables del chatbot documental.</summary>
public static class ChatbotErrorCodes
{
    public const string PreguntaInvalida = "chatbot.pregunta_invalida";
    public const string CuerpoExcesivo = "chatbot.cuerpo_excesivo";
    public const string AiNoConfigurada = "chatbot.ai_no_configurada";
    public const string AiNoDisponible = "chatbot.ai_no_disponible";
    public const string AiTimeout = "chatbot.ai_timeout";
    public const string AiRespuestaInvalida = "chatbot.ai_respuesta_invalida";
    public const string AiOcupado = "chatbot.ai_ocupado";
    public const string QdrantNoDisponible = "chatbot.qdrant_no_disponible";
    public const string QdrantTimeout = "chatbot.qdrant_timeout";
    public const string ErrorInterno = "chatbot.error_interno";
}

/// <summary>Pregunta enviada por el usuario al chatbot público.</summary>
public sealed record PreguntaChatbotDto
{
    public string Pregunta { get; init; } = string.Empty;
}

/// <summary>
/// Respuesta pública del chatbot. Solo expone texto y fuentes documentales citadas;
/// no incluye puntuaciones internas, ids de Qdrant, tokens ni detalles del proveedor.
/// </summary>
public sealed record RespuestaChatbotDto
{
    public string Respuesta { get; init; } = string.Empty;

    public bool SuficienteInformacion { get; init; }

    public IReadOnlyList<FuenteChatbotDto> Fuentes { get; init; }
        = Array.Empty<FuenteChatbotDto>();
}

/// <summary>Fuente documental citada en la respuesta, con su cita [S#].</summary>
public sealed record FuenteChatbotDto
{
    public string Id { get; init; } = string.Empty;

    public string Titulo { get; init; } = string.Empty;

    public string Fuente { get; init; } = string.Empty;

    public string Categoria { get; init; } = string.Empty;

    public double Relevancia { get; init; }
}
