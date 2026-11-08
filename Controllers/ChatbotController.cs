using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PROYECTO_FINAL.Models.Api;
using PROYECTO_FINAL.Services;

namespace PROYECTO_FINAL.Controllers;

[ApiController]
[AllowAnonymous]
[Route("Chatbot")]
[ChatbotAntiforgery]
public sealed class ChatbotController(ChatbotApiClient chatbotApiClient) : Controller
{
    [HttpPost("Preguntar")]
    [Produces("application/json")]
    public async Task<IActionResult> Preguntar(
        [FromBody] ChatbotProxyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ChatbotProxyError
            {
                Codigo = "chatbot.pregunta_invalida",
                Mensaje = "La pregunta debe tener entre 3 y 500 caracteres."
            });
        }

        var result = await chatbotApiClient.PreguntarAsync(request, cancellationToken);

        return result.Status switch
        {
            ChatbotApiCallStatus.Success => Ok(result.Response),
            ChatbotApiCallStatus.Canceled => new EmptyResult(),
            ChatbotApiCallStatus.ClientTimeout => StatusCode(
                StatusCodes.Status504GatewayTimeout,
                Error("chatbot.proxy_timeout", "El asistente tardó demasiado en responder.")),
            ChatbotApiCallStatus.ConnectionError => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                Error("chatbot.api_no_disponible", "El asistente no está disponible en este momento.")),
            ChatbotApiCallStatus.ApiTimeout => StatusCode(
                StatusCodes.Status504GatewayTimeout,
                Error("chatbot.api_timeout", "El asistente tardó demasiado en responder.")),
            _ => StatusCode(
                (int)(result.ApiStatusCode ?? System.Net.HttpStatusCode.BadGateway),
                Error(MapErrorCode(result), MapErrorMessage(result)))
        };
    }

    private static ChatbotProxyError Error(string code, string message) => new()
    {
        Codigo = code,
        Mensaje = message
    };

    private static string MapErrorCode(ChatbotApiResult result) =>
        result.ApiStatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => "chatbot.rate_limit",
            System.Net.HttpStatusCode.BadGateway => "chatbot.ai_error",
            System.Net.HttpStatusCode.ServiceUnavailable => "chatbot.no_disponible",
            _ => "chatbot.error"
        };

    private static string MapErrorMessage(ChatbotApiResult result) =>
        result.ApiStatusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => "La pregunta no es válida.",
            System.Net.HttpStatusCode.TooManyRequests => "El asistente está recibiendo muchas solicitudes. Intenta nuevamente en unos segundos.",
            System.Net.HttpStatusCode.BadGateway => "El asistente no pudo generar una respuesta válida.",
            _ => "El asistente no está disponible en este momento."
        };
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class ChatbotAntiforgeryAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ChatbotProxyError
            {
                Codigo = "chatbot.antiforgery_invalido",
                Mensaje = "No se pudo validar la solicitud. Recarga la página e inténtalo nuevamente."
            });
        }
    }
}
