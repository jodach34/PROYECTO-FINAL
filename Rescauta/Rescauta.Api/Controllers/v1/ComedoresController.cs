using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Api.Security;
using Rescauta.Application.Features.Comedores;
using Rescauta.Application.Features.Comedores.Dto;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints del mapa de comedores. Son los que consume la pantalla de Mapa.
///
///   GET  /api/v1/comedores                lista con estado de abastecimiento
///   GET  /api/v1/comedores/{id}           ficha con el inventario del comedor
///   GET  /api/v1/comedores/{id}/kardex    movimientos recientes, para el Panel
///   POST /api/v1/comedores                alta de un comedor (solo Administrador)
///
/// Lo que cambia el inventario es KardexController, y por eso el boton "Registrar Consumo
/// Diario" del Panel va ahi.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ComedoresController(IComedorService comedorService) : ApiControllerBase
{
    /// <summary>Registra un comedor nuevo. Solo el Administrador puede darlo de alta.</summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.Admin)]
    [ProducesResponseType<ComedorResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ComedorResultDto>> Crear(
        [FromBody] CrearComedorDto comedor,
        CancellationToken cancellationToken)
    {
        var result = await comedorService.CrearAsync(comedor, cancellationToken);

        if (result.IsFailure)
        {
            return FromResult(result);
        }

        Response.Headers.Location = $"/api/v1/comedores/{result.Value.Id}";

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

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
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<IReadOnlyList<KardexFilaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KardexFilaDto>>> ObtenerKardex(
        Guid comedorId,
        [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        var result = await comedorService.ObtenerKardexAsync(
            comedorId,
            usuario,
            limite,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        if (result.ErrorCode == ComedorErrorCodes.SinPermiso)
        {
            return ConstruirProblem(
                result.ErrorCode,
                result.ErrorMessage,
                StatusCodes.Status403Forbidden);
        }

        return ConstruirProblem(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Cuerpo ProblemDetails coherente con el de la base. El estado se elige segun el
    /// motivo: 403 cuando el comedor existe pero no esta asignado al usuario, 404 cuando
    /// el recurso no existe.
    /// </summary>
    private ObjectResult ConstruirProblem(
        string? errorCode,
        string? errorMessage,
        int statusCode = StatusCodes.Status404NotFound)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = errorCode ?? "No encontrado",
            Detail = errorMessage,
            Instance = HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(statusCode, problem);
    }
}
