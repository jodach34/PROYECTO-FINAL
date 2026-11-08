namespace Rescauta.Application.Features.Ai.Models;

/// <summary>
/// Prompt final listo para el proveedor. La instrucción de sistema es estática y
/// el prompt de usuario contiene la pregunta y los documentos ya delimitados.
/// </summary>
public sealed record RagPrompt(
    string SystemInstruction,
    string UserPrompt);
