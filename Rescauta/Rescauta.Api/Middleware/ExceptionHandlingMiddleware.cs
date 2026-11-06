using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Api.Middleware;

/// <summary>
/// Traductor unico de excepciones a respuestas HTTP. Sin esto cada controller
/// tendria su propio try/catch y los 3 devs el problema de forma distinta.
///
/// Reglas:
///   DomainException ........ 400 (regla de negocio violada)
///   DbUpdateConcurrency .... 409 (otro dev actualizo el registro antes)
///   KeyNotFoundException ... 404
///   FluentValidation ....... 400 (via ModelState, manejado por el API Controller)
///   resto .................. 500 (log Warning si es Domain, Error si es infraestructura)
///
/// Importante: el detalle de los 500 jamas se expone al cliente en produccion.
/// Se devuelve el correlation id del log y nada mas.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message) = Map(exception);

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Error de negocio en {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            type = $"https://rescauta.org/errors/{code}",
            title = code,
            status = (int)statusCode,
            detail = message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    private static (HttpStatusCode StatusCode, string Code, string Message) Map(Exception exception) => exception switch
    {
        DomainException domain => (HttpStatusCode.BadRequest, "Regla_de_Negocio", domain.Message),

        DbUpdateConcurrencyException => (
            HttpStatusCode.Conflict,
            "Conflicto_de_Concurrencia",
            "El registro fue modificado por otra operacion. Vuelva a intentarlo."),

        DbUpdateException => (
            HttpStatusCode.Conflict,
            "Error_de_Persistencia",
            "No se pudo guardar la operacion."),

        KeyNotFoundException => (
            HttpStatusCode.NotFound,
            "No_Encontrado",
            "El recurso solicitado no existe."),

        OperationCanceledException => (
            (HttpStatusCode)499,
            "Peticion_Cancelada",
            "El cliente cancelo la peticion."),

        _ => (
            HttpStatusCode.InternalServerError,
            "Error_Interno",
            "Ocurrio un error inesperado. Contacte a soporte con el traceId.")
    };
}
