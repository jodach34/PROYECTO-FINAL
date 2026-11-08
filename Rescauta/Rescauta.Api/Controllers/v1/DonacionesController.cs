using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Api.Security;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Donaciones;
using Rescauta.Application.Features.Donaciones.Dto;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints de donacion.
///
/// Flujo:
///   * POST /donaciones registra la donacion en estado Pendiente. No mueve stock.
///   * POST /donaciones/{id}/confirmar confirma la recepcion y asienta la entrada al kardex.
///   * Los endpoints de gestion filtran por los comedores asignados al operador.
///
/// El POST de registro ya no devuelve una donacion "confirmada": el stock entra recien al
/// confirmar, que es una accion separada de la coordinacion.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class DonacionesController(IDonacionService donacionService) : ApiControllerBase
{
    /// <summary>Donaciones registradas, de la mas reciente a la mas antigua.</summary>
    [HttpGet]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<IReadOnlyList<DonacionResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DonacionResultDto>>> Listar(
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromResult(await donacionService.ObtenerAsync(
            usuario,
            limite,
            cancellationToken));
    }

    [HttpGet("mias")]
    [Authorize(Policy = AuthPolicies.Donar)]
    [ProducesResponseType<IReadOnlyList<DonacionResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DonacionResultDto>>> MisDonaciones(
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = UsuarioIdActual();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        return FromResult(await donacionService.ObtenerPorUsuarioAsync(
            usuarioId.Value,
            limite,
            cancellationToken));
    }

    /// <summary>
    /// Busca una donacion por su codigo publico de seguimiento. Es la lectura que usa el
    /// donante desde la pagina de Seguimiento. Devuelve una proyeccion publica minima.
    /// </summary>
    [HttpGet("seguimiento/{codigo}")]
    [AllowAnonymous]
    [ProducesResponseType<SeguimientoDonacionPublicoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeguimientoDonacionPublicoDto>> ObtenerPorCodigo(
        string codigo,
        CancellationToken cancellationToken)
    {
        return FromDonacionResult(
            await donacionService.ObtenerPorCodigoAsync(codigo, cancellationToken));
    }

    /// <summary>Listado administrativo paginado y filtrado por comedor.</summary>
    [HttpGet("gestion")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<PagedResult<DonacionResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DonacionResultDto>>> Buscar(
        [FromQuery] Rescauta.Domain.Entities.Donaciones.EstadoDonacion? estado,
        [FromQuery] Guid? comedorId,
        [FromQuery] Guid? insumoId,
        [FromQuery] string? busqueda,
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        var filtro = new DonacionFiltroDto
        {
            Estado = estado,
            ComedorId = comedorId,
            InsumoId = insumoId,
            Busqueda = busqueda,
            Desde = desde,
            Hasta = hasta,
            Pagina = pagina,
            TamanoPagina = tamanoPagina
        };

        return FromDonacionResult(
            await donacionService.BuscarAsync(filtro, usuario, cancellationToken));
    }

    [HttpGet("{donacionId:guid}")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonacionResultDto>> ObtenerPorId(
        Guid donacionId,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.ObtenerPorIdAsync(donacionId, usuario, cancellationToken));
    }

    [HttpGet("{donacionId:guid}/historial")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<IReadOnlyList<DonacionHistorialDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DonacionHistorialDto>>> ObtenerHistorial(
        Guid donacionId,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.ObtenerHistorialAsync(donacionId, usuario, cancellationToken));
    }

    [HttpPost("{donacionId:guid}/confirmar")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DonacionResultDto>> Confirmar(
        Guid donacionId,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.ConfirmarAsync(donacionId, usuario, cancellationToken));
    }

    [HttpPost("{donacionId:guid}/en-transito")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DonacionResultDto>> MarcarEnTransito(
        Guid donacionId,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.MarcarEnTransitoAsync(donacionId, usuario, cancellationToken));
    }

    [HttpPost("{donacionId:guid}/entregar")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DonacionResultDto>> Entregar(
        Guid donacionId,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.EntregarAsync(donacionId, usuario, cancellationToken));
    }

    [HttpPost("{donacionId:guid}/cancelar")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DonacionResultDto>> Cancelar(
        Guid donacionId,
        [FromBody] MotivoDonacionDto motivo,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.CancelarAsync(
                donacionId,
                motivo,
                usuario,
                cancellationToken));
    }

    [HttpPost("{donacionId:guid}/anular")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DonacionResultDto>> Anular(
        Guid donacionId,
        [FromBody] MotivoDonacionDto motivo,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromDonacionResult(
            await donacionService.AnularAsync(
                donacionId,
                motivo,
                usuario,
                cancellationToken));
    }

    [HttpDelete("{donacionId:guid}")]
    [Authorize(Policy = AuthPolicies.Operacion)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Eliminar(
        Guid donacionId,
        CancellationToken cancellationToken)
    {
        var usuario = UsuarioActual();

        if (usuario is null)
        {
            return Unauthorized();
        }

        return FromResult(await donacionService.EliminarAsync(
            donacionId,
            usuario,
            cancellationToken));
    }

    /// <summary>
    /// Registra la donacion. El servidor genera el codigo de seguimiento y lo devuelve: el
    /// cliente no lo manda, porque es el indice unico de la tabla.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.Donar)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DonacionResultDto>> Registrar(
        [FromBody] RegistrarDonacionDto donacion,
        CancellationToken cancellationToken)
    {
        var usuarioId = UsuarioIdActual();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var datos = string.IsNullOrWhiteSpace(donacion.Donante)
            ? donacion with { Donante = User.Identity?.Name ?? string.Empty }
            : donacion;

        var result = await donacionService.RegistrarAsync(datos, usuarioId, cancellationToken);

        if (result.IsFailure)
        {
            return FromDonacionResult(result);
        }

        Response.Headers.Location = $"/api/v1/donaciones/{result.Value.DonacionId}";

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private Guid? UsuarioIdActual() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private ActionResult<T> FromDonacionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return StatusCode(
            ObtenerStatus(result.ErrorCode),
            CrearProblem(result.ErrorCode, result.ErrorMessage));
    }

    private ActionResult FromDonacionResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return StatusCode(
            ObtenerStatus(result.ErrorCode),
            CrearProblem(result.ErrorCode, result.ErrorMessage));
    }

    private static int ObtenerStatus(string? errorCode) => errorCode switch
    {
        DonacionErrorCodes.DonacionNoEncontrada => StatusCodes.Status404NotFound,
        DonacionErrorCodes.InsumoNoEncontrado => StatusCodes.Status404NotFound,
        DonacionErrorCodes.SinPermiso => StatusCodes.Status403Forbidden,
        DonacionErrorCodes.ConflictoConcurrente => StatusCodes.Status409Conflict,
        DonacionErrorCodes.StockInsuficiente => StatusCodes.Status409Conflict,
        DonacionErrorCodes.EstadoInvalido => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private ProblemDetails CrearProblem(string? errorCode, string? errorMessage) =>
        new()
        {
            Status = ObtenerStatus(errorCode),
            Title = errorCode ?? "Error",
            Detail = errorMessage,
            Instance = HttpContext.Request.Path,
            Extensions = { ["traceId"] = HttpContext.TraceIdentifier }
        };
}
