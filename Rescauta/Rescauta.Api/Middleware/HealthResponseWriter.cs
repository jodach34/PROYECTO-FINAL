using System.Net.Mime;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Rescauta.Api.Middleware;

/// <summary>
/// Escribe la respuesta de /health en JSON. Formato estable para que los contenedores
/// y los tableros de monitoreo puedan leerla sin parsear texto.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var payload = new
        {
            Status = report.Status.ToString(),
            TotalDurationInMilliseconds = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(entry => new
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description,
                DurationInMilliseconds = entry.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
