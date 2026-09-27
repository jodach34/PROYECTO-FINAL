using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Application.Features.Comedores;
using Rescauta.Application.Features.Comedores.Dto;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints de lectura del mapa de comedores. Son los que consume la pantalla de Mapa.
///
///   GET /api/v1/comedores                lista con estado de abastecimiento
///   GET /api/v1/comedores/{id}           ficha con el inventario del comedor
///   GET /api/v1/comedores/{id}/kardex    movimientos recientes, para el Panel
///
/// No hay verbos POST aqui: el modulo Comedores no escribe. Lo que cambia el inventario es
/// KardexController, y por eso el boton "Registrar Consumo Diario" del Panel va ahi.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ComedoresController(IComedorService comedorService) : ApiControllerBase
{
    /// <summary>
    /// Comedores con su porcentaje de abastecimiento, del mas urgente al menos urgente.
    /// Es lo que pintan los pines del mapa y las tarjetas del sidebar.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ComedorResumenDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComedorResumenDto>>> Listar(
        CancellationToken cancellationToken)
    {
        return FromResult(await comedorService.ObtenerResumenAsync(cancellationToken));
    }

    /// <summary>
    /// Ficha de un comedor: direccion, contacto y su despensa insumo por insumo con el
    /// porcentaje de cada uno. Un 404 (no un 400) cuando el id no existe, que es lo que
    /// espera un cliente que navega por ids.
    /// </summary>
    [HttpGet("{comedorId:guid}")]
    [ProducesResponseType<ComedorDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComedorDetalleDto>> Obtener(
        Guid comedorId,
        CancellationToken cancellationToken)
    {
        var result = await comedorService.ObtenerDetalleAsync(comedorId, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(ConstruirProblem(result.ErrorCode, result.ErrorMessage));
    }

    /// <summary>
    /// Kardex del comedor, del asiento mas reciente al mas antiguo. Es la tabla del Panel de
    /// Control.
    /// </summary>
    [HttpGet("{comedorId:guid}/kardex")]
    [ProducesResponseType<IReadOnlyList<KardexFilaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KardexFilaDto>>> ObtenerKardex(
        Guid comedorId,
        [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await comedorService.ObtenerKardexAsync(comedorId, limite, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(ConstruirProblem(result.ErrorCode, result.ErrorMessage));
    }

    /// <summary>
    /// 404 con el mismo cuerpo ProblemDetails que usa la base para los 400, para que el
    /// cliente lea el motivo igual en los dos casos. La base no lo ofrece porque su
    /// ProblemFrom siempre devuelve 400.
    /// </summary>
    private ObjectResult ConstruirProblem(string? errorCode, string? errorMessage)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = errorCode ?? "No encontrado",
            Detail = errorMessage,
            Instance = HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return NotFound(problem);
    }
}
