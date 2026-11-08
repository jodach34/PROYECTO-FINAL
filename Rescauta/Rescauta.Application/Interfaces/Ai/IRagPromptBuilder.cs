using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Construye la instrucción de sistema y el prompt de usuario.
/// La instrucción de sistema es estática; la pregunta y los documentos se tratan
/// como datos no confiables y se delimitan/escapan antes de enviarse al modelo.
/// </summary>
public interface IRagPromptBuilder
{
    RagPrompt Build(string pregunta, RagContext context);
}
