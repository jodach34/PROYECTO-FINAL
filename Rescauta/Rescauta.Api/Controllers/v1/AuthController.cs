using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Rescauta.Api.Security;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Autenticacion;
using Rescauta.Application.Features.Autenticacion.Dto;

namespace Rescauta.Api.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class AuthController(
    IAutenticacionService autenticacionService,
    IConfiguration configuration) : ApiControllerBase
{
    [HttpPost("registro/donante")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<UsuarioSesionDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UsuarioSesionDto>> RegistrarDonante(
        [FromBody] RegistrarDonanteDto solicitud,
        CancellationToken cancellationToken)
    {
        var resultado = await autenticacionService.RegistrarDonanteAsync(solicitud, cancellationToken);

        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : FromAuthFailure(resultado);
    }

    [HttpPost("registro/operador")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<UsuarioSesionDto>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<UsuarioSesionDto>> SolicitarOperador(
        [FromBody] SolicitarOperadorDto solicitud,
        CancellationToken cancellationToken)
    {
        var resultado = await autenticacionService.SolicitarOperadorAsync(solicitud, cancellationToken);

        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status202Accepted, resultado.Value)
            : FromAuthFailure(resultado);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<LoginResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResultDto>> Login(
        [FromBody] LoginDto solicitud,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var resultado = await autenticacionService.LoginAsync(
            solicitud,
            ip,
            userAgent,
            cancellationToken);

        return resultado.IsSuccess ? Ok(resultado.Value) : FromAuthFailure(resultado);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = TokenActual();

        if (token is null)
        {
            return NoContent();
        }

        var resultado = await autenticacionService.LogoutAsync(token, cancellationToken);
        return FromResult(resultado);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UsuarioSesionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UsuarioSesionDto>> Me(CancellationToken cancellationToken)
    {
        var token = TokenActual();

        if (token is null)
        {
            return Unauthorized();
        }

        var resultado = await autenticacionService.ValidarSesionAsync(token, cancellationToken);
        return resultado.IsSuccess ? Ok(resultado.Value) : FromAuthFailure(resultado);
    }

    [HttpPost("recuperar")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<RecuperacionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecuperacionResultDto>> Recuperar(
        [FromBody] RecuperarPasswordDto solicitud,
        CancellationToken cancellationToken)
    {
        var url = configuration["Auth:ResetPasswordUrl"] ?? "http://localhost:5142/Cuenta/RestablecerPassword";
        var resultado = await autenticacionService.SolicitarRecuperacionAsync(
            solicitud,
            url,
            cancellationToken);

        return resultado.IsSuccess ? Ok(resultado.Value) : FromAuthFailure(resultado);
    }

    [HttpPost("restablecer")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Restablecer(
        [FromBody] RestablecerPasswordDto solicitud,
        CancellationToken cancellationToken)
    {
        var resultado = await autenticacionService.RestablecerPasswordAsync(
            solicitud,
            cancellationToken);

        return resultado.IsSuccess ? NoContent() : FromAuthFailure(resultado);
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<IActionResult> CambiarPassword(
        [FromBody] CambiarPasswordDto solicitud,
        CancellationToken cancellationToken)
    {
        var id = UsuarioIdActual();

        if (id is null)
        {
            return Unauthorized();
        }

        var resultado = await autenticacionService.CambiarPasswordAsync(
            id.Value,
            solicitud,
            cancellationToken);

        return resultado.IsSuccess ? NoContent() : FromAuthFailure(resultado);
    }

    private string? TokenActual()
    {
        var header = Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    private Guid? UsuarioIdActual() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private ObjectResult FromAuthFailure<T>(Result<T> resultado)
    {
        var status = resultado.ErrorCode switch
        {
            AuthErrorCodes.CredencialesInvalidas or AuthErrorCodes.NoAutorizado => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.CuentaPendiente or AuthErrorCodes.CuentaRechazada or AuthErrorCodes.CuentaBloqueada => StatusCodes.Status403Forbidden,
            AuthErrorCodes.CorreoRegistrado => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: status,
            title: resultado.ErrorCode,
            detail: resultado.ErrorMessage);
    }

    private ObjectResult FromAuthFailure(Result resultado)
    {
        var status = resultado.ErrorCode switch
        {
            AuthErrorCodes.CredencialesInvalidas or AuthErrorCodes.NoAutorizado => StatusCodes.Status401Unauthorized,
            AuthErrorCodes.TokenInvalido => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: status,
            title: resultado.ErrorCode,
            detail: resultado.ErrorMessage);
    }
}
