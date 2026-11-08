using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoint público del chatbot documental.
///
///   POST /api/v1/chatbot/preguntar
///
/// Es anónimo a propósito: el asistente responde solo con documentación pública.
/// No recibe tokens, no usa cookies y no puede escribir en Qdrant ni en SQLite.
/// El rate limiting y el límite de tamaño del cuerpo protegen el consumo de Gemini.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class ChatbotController(IRagAnswerService ragAnswerService) : ApiControllerBase
{
    /// <summary>
    /// Responde una pregunta usando exclusivamente la documentación institucional.
    /// Devuelve 200 incluso cuando no hay información suficiente; en ese caso
    /// <c>suficienteInformacion</c> es false y <c>fuentes</c> está vacío.
    /// </summary>
    [HttpPost("preguntar")]
    [EnableRateLimiting("chatbot")]
    [RequestSizeLimit(8 * 1024)]
    [ProducesResponseType<RespuestaChatbotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<RespuestaChatbotDto>> Preguntar(
        [FromBody] PreguntaChatbotDto request,
        CancellationToken cancellationToken)
    {
        var result = await ragAnswerService.PreguntarAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var status = MapStatus(result.ErrorCode);

        return Problem(
            status,
            result.ErrorCode ?? ChatbotErrorCodes.ErrorInterno,
            result.ErrorMessage ?? "No se pudo procesar la solicitud.");
    }

    private ObjectResult Problem(int status, string code, string message)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = message,
            Instance = HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(status, problem);
    }

    private static int MapStatus(string? errorCode) => errorCode switch
    {
        ChatbotErrorCodes.PreguntaInvalida => StatusCodes.Status400BadRequest,
        ChatbotErrorCodes.AiRespuestaInvalida => StatusCodes.Status502BadGateway,
        ChatbotErrorCodes.AiTimeout => StatusCodes.Status504GatewayTimeout,
        ChatbotErrorCodes.QdrantTimeout => StatusCodes.Status504GatewayTimeout,
        ChatbotErrorCodes.AiNoConfigurada => StatusCodes.Status503ServiceUnavailable,
        ChatbotErrorCodes.AiNoDisponible => StatusCodes.Status503ServiceUnavailable,
        ChatbotErrorCodes.QdrantNoDisponible => StatusCodes.Status503ServiceUnavailable,
        ChatbotErrorCodes.AiOcupado => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };
}
