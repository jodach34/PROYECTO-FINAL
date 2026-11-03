using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Api.Security;
using Rescauta.Application.Features.Donaciones;
using Rescauta.Application.Features.Donaciones.Dto;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints de donacion. Son los que consume el formulario del Banco de Alimentos.
///
///   GET  /api/v1/donaciones          lista las donaciones registradas
///   POST /api/v1/donaciones          registra una donacion y asienta la entrada en kardex
///
/// La entidad <c>Donacion</c> ya existia en el dominio pero no tenia controller: el modulo
/// estaba a medio hacer. Este controller es el que la cierra.
///
/// El POST devuelve 201 con la donacion YA confirmada, no un 202: el asiento del kardex se
/// escribe en la misma transaccion, asi que cuando el cliente recibe la respuesta la
/// mercancia ya esta en la despensa del comedor.
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
        return FromResult(await donacionService.ObtenerAsync(limite, cancellationToken));
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
    /// donante desde la pagina de Seguimiento; no expone la lista completa.
    /// </summary>
    [HttpGet("seguimiento/{codigo}")]
    [AllowAnonymous]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonacionResultDto>> ObtenerPorCodigo(
        string codigo,
        CancellationToken cancellationToken)
    {
        var result = await donacionService.ObtenerPorCodigoAsync(codigo, cancellationToken);

        if (result.IsFailure && result.ErrorCode == DonacionErrorCodes.DonacionNoEncontrada)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = result.ErrorCode,
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        return FromResult(result);
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
            return FromResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private Guid? UsuarioIdActual() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
