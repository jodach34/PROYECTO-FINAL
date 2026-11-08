using Microsoft.AspNetCore.Mvc;
using Rescauta.Application.Features.Ai.Dto;

namespace Rescauta.Api.Middleware;

/// <summary>
/// Límite explícito del cuerpo del chatbot, independiente del servidor.
///
/// <c>[RequestSizeLimit]</c> funciona en Kestrel, pero algunos hosts de prueba y
/// proxies no exponen <c>IHttpMaxRequestBodySizeFeature</c>. Este middleware
/// comprueba Content-Length antes del model binding y devuelve 413 con un
/// ProblemDetails estable, sin leer ni registrar el cuerpo.
///
/// No sustituye al límite del servidor: lo complementa. El límite del servidor
/// sigue siendo la defensa para cuerpos enviados sin Content-Length (chunked).
/// </summary>
public sealed class ChatbotRequestBodyLimitMiddleware(
    RequestDelegate next,
    ILogger<ChatbotRequestBodyLimitMiddleware> logger)
{
    public const long MaxBodyBytes = 8 * 1024;

    private static readonly PathString ChatbotPath = new("/api/v1/chatbot");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(ChatbotPath, StringComparison.OrdinalIgnoreCase) &&
            context.Request.ContentLength is long contentLength &&
            contentLength > MaxBodyBytes)
        {
            logger.LogWarning(
                "Solicitud de chatbot rechazada: el cuerpo declarado supera el límite permitido.");

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = ChatbotErrorCodes.CuerpoExcesivo,
                Detail = "El cuerpo de la solicitud es demasiado grande.",
                Instance = context.Request.Path,
                Extensions =
                {
                    ["traceId"] = context.TraceIdentifier
                }
            });

            return;
        }

        await next(context);
    }
}
