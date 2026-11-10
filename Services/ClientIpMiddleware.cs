using System.Net;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Deja en HttpContext.Connection.RemoteIpAddress la IP real del navegador cuando la
/// peticion llega a traves del proxy de Render y Cloudflare. Se ejecuta despues de
/// UseForwardedHeaders (que solo se encarga del esquema https) y antes de cualquier
/// componente que use la IP, como el rate limiting o los registros de sesion.
/// </summary>
public sealed class ClientIpMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    private readonly IReadOnlyCollection<IPNetwork> _redesConfianza =
        TrustedProxyIp.LeerRedesConfianza(configuration);

    public Task InvokeAsync(HttpContext context)
    {
        var ip = TrustedProxyIp.ResolverCliente(context, _redesConfianza);

        if (ip is not null)
        {
            context.Connection.RemoteIpAddress = ip;
        }

        return next(context);
    }
}
