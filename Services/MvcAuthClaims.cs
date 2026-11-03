using System.Security.Claims;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Services;

public static class MvcAuthClaims
{
    public static ClaimsPrincipal Crear(UsuarioSesion usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, usuario.Rol),
            new("estado", usuario.Estado)
        };

        if (!string.IsNullOrWhiteSpace(usuario.Organizacion))
        {
            claims.Add(new Claim("organizacion", usuario.Organizacion));
        }

        foreach (var comedor in usuario.Comedores)
        {
            claims.Add(new Claim("comedor_id", comedor.Id.ToString()));
        }

        var identidad = new ClaimsIdentity(
            claims,
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identidad);
    }

    public static ClaimsPrincipal Actualizar(ClaimsPrincipal actual, UsuarioSesion usuario) =>
        Crear(usuario);
}
