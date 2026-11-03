using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Autenticacion;

namespace Rescauta.Api.Security;

public sealed class AuthSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAutenticacionService autenticacionService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("Token vacio.");
        }

        var resultado = await autenticacionService.ValidarSesionAsync(token, Context.RequestAborted);

        if (resultado.IsFailure)
        {
            return AuthenticateResult.Fail(resultado.ErrorMessage ?? "Sesion no valida.");
        }

        var usuario = resultado.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, usuario.Rol),
            new("estado", usuario.Estado)
        };

        foreach (var comedor in usuario.Comedores)
        {
            claims.Add(new Claim("comedor_id", comedor.Id.ToString()));
        }

        var identidad = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identidad);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
