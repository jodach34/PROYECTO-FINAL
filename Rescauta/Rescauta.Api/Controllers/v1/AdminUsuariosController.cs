using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Security;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Usuarios;
using Rescauta.Application.Features.Usuarios.Dto;

namespace Rescauta.Api.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/usuarios")]
[Authorize(Policy = AuthPolicies.Admin)]
public sealed class AdminUsuariosController(IUsuarioAdminService usuarioService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UsuarioAdminDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UsuarioAdminDto>>> Listar(
        [FromQuery] string? busqueda,
        CancellationToken cancellationToken)
    {
        return FromResult(await usuarioService.ListarUsuariosAsync(busqueda, cancellationToken));
    }

    [HttpGet("solicitudes")]
    [ProducesResponseType<IReadOnlyList<SolicitudOperadorAdminDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SolicitudOperadorAdminDto>>> Solicitudes(
        CancellationToken cancellationToken)
    {
        return FromResult(await usuarioService.ListarSolicitudesPendientesAsync(cancellationToken));
    }

    [HttpGet("comedores")]
    [ProducesResponseType<IReadOnlyList<ComedorOpcionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComedorOpcionDto>>> Comedores(
        CancellationToken cancellationToken)
    {
        return FromResult(await usuarioService.ListarComedoresAsync(cancellationToken));
    }

    [HttpGet("{usuarioId:guid}")]
    [ProducesResponseType<UsuarioAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioAdminDto>> Obtener(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        return FromAdminResult(await usuarioService.ObtenerUsuarioAsync(usuarioId, cancellationToken));
    }

    [HttpPost("{usuarioId:guid}/aprobar")]
    public async Task<IActionResult> Aprobar(
        Guid usuarioId,
        [FromBody] AprobarOperadorDto solicitud,
        CancellationToken cancellationToken)
    {
        var adminId = UsuarioIdActual();

        if (adminId is null)
        {
            return Unauthorized();
        }

        return FromAdminResult(await usuarioService.AprobarOperadorAsync(
            usuarioId,
            adminId.Value,
            solicitud,
            cancellationToken));
    }

    [HttpPost("{usuarioId:guid}/rechazar")]
    public async Task<IActionResult> Rechazar(
        Guid usuarioId,
        [FromBody] RechazarSolicitudDto solicitud,
        CancellationToken cancellationToken)
    {
        var adminId = UsuarioIdActual();

        if (adminId is null)
        {
            return Unauthorized();
        }

        return FromAdminResult(await usuarioService.RechazarSolicitudAsync(
            usuarioId,
            adminId.Value,
            solicitud,
            cancellationToken));
    }

    [HttpPut("{usuarioId:guid}/rol")]
    public async Task<IActionResult> CambiarRol(
        Guid usuarioId,
        [FromBody] CambiarRolDto solicitud,
        CancellationToken cancellationToken)
    {
        var adminId = UsuarioIdActual();

        if (adminId is null)
        {
            return Unauthorized();
        }

        return FromAdminResult(await usuarioService.CambiarRolAsync(
            usuarioId,
            adminId.Value,
            solicitud,
            cancellationToken));
    }

    [HttpPut("{usuarioId:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(
        Guid usuarioId,
        [FromBody] CambiarEstadoUsuarioDto solicitud,
        CancellationToken cancellationToken)
    {
        var adminId = UsuarioIdActual();

        if (adminId is null)
        {
            return Unauthorized();
        }

        return FromAdminResult(await usuarioService.CambiarEstadoAsync(
            usuarioId,
            adminId.Value,
            solicitud,
            cancellationToken));
    }

    [HttpPut("{usuarioId:guid}/comedores")]
    public async Task<IActionResult> ActualizarComedores(
        Guid usuarioId,
        [FromBody] ActualizarAsignacionesDto solicitud,
        CancellationToken cancellationToken)
    {
        return FromAdminResult(await usuarioService.ActualizarAsignacionesAsync(
            usuarioId,
            solicitud,
            cancellationToken));
    }

    private Guid? UsuarioIdActual() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private ActionResult FromAdminResult(Result resultado)
    {
        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        var status = resultado.ErrorCode == UsuarioAdminErrorCodes.NoEncontrado ||
                     resultado.ErrorCode == UsuarioAdminErrorCodes.SolicitudNoEncontrada
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Problem(
            statusCode: status,
            title: resultado.ErrorCode,
            detail: resultado.ErrorMessage);
    }

    private ActionResult<T> FromAdminResult<T>(Result<T> resultado)
    {
        if (resultado.IsSuccess)
        {
            return Ok(resultado.Value);
        }

        var status = resultado.ErrorCode == UsuarioAdminErrorCodes.NoEncontrado
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return Problem(
            statusCode: status,
            title: resultado.ErrorCode,
            detail: resultado.ErrorMessage);
    }
}
