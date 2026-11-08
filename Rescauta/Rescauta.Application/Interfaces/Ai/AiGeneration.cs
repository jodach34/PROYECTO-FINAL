namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Petición de generación de texto al proveedor de IA.
/// Las instrucciones de sistema son estáticas y controladas por la aplicación;
/// el prompt de usuario contiene la pregunta y el contexto documental ya delimitado.
/// </summary>
public sealed record AiGenerationRequest
{
    public string SystemInstruction { get; init; } = string.Empty;

    public string UserPrompt { get; init; } = string.Empty;

    public double Temperature { get; init; } = 0.2;

    public int MaxOutputTokens { get; init; } = 700;
}

/// <summary>
/// Respuesta sanitizada de una generación. No incluye cabeceras, claves ni el cuerpo
/// completo de un error del proveedor.
/// </summary>
public sealed record AiGenerationResult(
    string Text,
    string FinishReason,
    int PromptTokens,
    int CandidateTokens,
    int TotalTokens);
