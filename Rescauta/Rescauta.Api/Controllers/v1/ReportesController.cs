using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Api.Security;
using Rescauta.Application.Features.Reportes;
using Rescauta.Application.Features.Reportes.Dto;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Reportes basicos de donaciones y stock.
///
///   GET /api/v1/reportes/donaciones-recibidas
///   GET /api/v1/reportes/stock-disponible
///
/// El Administrador ve toda la red; Operador y Coordinador solo sus comedores asignados.
/// El controller no calcula nada: delega el alcance y la agregacion en Application.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = AuthPolicies.Operacion)]
public sealed class ReportesController(IReporteService reporteService) : ApiControllerBase
{
    /// <summary>Total de donaciones recibidas y cantidad total recibida.</summary>
    [HttpGet("donaciones-recibidas")]
    [ProducesResponseType<ReporteDonacionesRecibidasDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteDonacionesRecibidasDto>> DonacionesRecibidas(
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromResult(await reporteService.ObtenerDonacionesRecibidasAsync(
            usuario,
            cancellationToken));
    }

    /// <summary>Stock disponible agrupado por nombre y unidad de medida del insumo.</summary>
    [HttpGet("stock-disponible")]
    [ProducesResponseType<IReadOnlyList<ReporteStockInsumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReporteStockInsumoDto>>> StockDisponible(
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromResult(await reporteService.ObtenerStockDisponibleAsync(
            usuario,
            cancellationToken));
    }
}
