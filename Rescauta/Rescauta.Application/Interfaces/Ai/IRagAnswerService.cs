using Rescauta.Application.Common;
using Rescauta.Application.Features.Ai.Dto;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Caso de uso público del chatbot documental: valida la pregunta, recupera contexto,
/// genera una respuesta fundamentada y devuelve únicamente fuentes citadas del contexto.
/// </summary>
public interface IRagAnswerService
{
    Task<Result<RespuestaChatbotDto>> PreguntarAsync(
        PreguntaChatbotDto request,
        CancellationToken cancellationToken = default);
}
