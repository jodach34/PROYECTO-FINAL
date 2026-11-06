using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Application.Interfaces;
using Rescauta.Api.Controllers;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints de diagnostico del cascarón. No es logica de negocio: sirve para que
/// cada dev verifique de un vistazo que su maquina tiene EF Core, Redis y SignalR
/// funcionando antes de empezar su modulo.
///
///   GET /api/v1/system/readiness  -> comprobacion profunda (base + cache)
///   GET /api/v1/system/info       -> metadatos de la instancia
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class SystemController(ISystemReadinessService readinessService) : ApiControllerBase
{
    /// <summary>Comprueba que la base de datos y la cache responden.</summary>
    [HttpGet("readiness")]
    [ProducesResponseType<ReadinessReport>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReadinessReport>> GetReadiness(CancellationToken cancellationToken)
    {
        var report = await readinessService.GetReadinessAsync(cancellationToken);

        // 200 si todo esta bien, 503 si la base de datos no responde. La cache degradada
        // no tumbate el servicio: el equipo igual puede seguir desarrollando.
        return report.IsReady ? Ok(report) : StatusCode(StatusCodes.Status503ServiceUnavailable, report);
    }

    /// <summary>Metadatos de la instancia en ejecucion.</summary>
    [HttpGet("info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetInfo() => Ok(new
    {
        Service = "Rescauta API",
        Version = typeof(SystemController).Assembly.GetName().Version?.ToString(),
        Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
        UtcNow = DateTimeOffset.UtcNow,
        Modules = new[] { "Mapas", "Kardex", "Donaciones" }
    });
}
