using System.Net;
using System.Security.Cryptography;
using System.Text;
using Rescauta.Api.Security;

namespace Rescauta.Api.Middleware;

/// <summary>
/// Resuelve la IP real del cliente antes del rate limiting y de los registros de sesion.
///
/// Prioridad:
///   1. X-Rescauta-Client-Ip, solo si X-Rescauta-Internal-Key coincide con Internal:ApiKey.
///      Es el camino cuando el MVC llama a la API en nombre del navegador.
///   2. CF-Connecting-IP, cuando la conexion inmediata es un proxy privado de Render.
///   3. La IP de la conexion (proxy o local) como ultimo recurso.
///
/// Si llega una clave interna distinta de la configurada, la solicitud se rechaza con
/// 401. No se cae a CF-Connecting-IP: una clave invalida no debe degradar la proteccion.
/// </summary>
public sealed class ClientIpMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<ClientIpMiddleware> logger)
{
    private const string ClientIpHeader = "X-Rescauta-Client-Ip";
    private const string InternalKeyHeader = "X-Rescauta-Internal-Key";

    private readonly IReadOnlyCollection<IPNetwork> _redesConfianza =
        TrustedProxyIp.LeerRedesConfianza(configuration);

    public async Task InvokeAsync(HttpContext context)
    {
        var conexion = TrustedProxyIp.Normalizar(context.Connection.RemoteIpAddress);

        if (TrustedProxyIp.EsProxyConfiable(conexion, _redesConfianza))
        {
            var claveEsperada = configuration["Internal:ApiKey"];
            var claveRecibida = context.Request.Headers[InternalKeyHeader].ToString();

            if (!string.IsNullOrWhiteSpace(claveEsperada) &&
                !string.IsNullOrWhiteSpace(claveRecibida) &&
                !ClavesCoinciden(claveRecibida, claveEsperada))
            {
                logger.LogWarning(
                    "Se rechazo una solicitud desde {RemoteIp} porque {Header} no coincide con Internal:ApiKey.",
                    conexion,
                    InternalKeyHeader);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var ip = ResolverCliente(context, claveEsperada, claveRecibida);

            if (ip is not null)
            {
                context.Connection.RemoteIpAddress = ip;
            }
        }

        await next(context);
    }

    private static IPAddress? ResolverCliente(
        HttpContext context,
        string? claveEsperada,
        string claveRecibida)
    {
        if (!string.IsNullOrWhiteSpace(claveEsperada) &&
            !string.IsNullOrWhiteSpace(claveRecibida))
        {
            var ipInterna = TrustedProxyIp.LeerCabecera(
                context.Request.Headers[ClientIpHeader].ToString());

            if (ipInterna is not null)
            {
                return ipInterna;
            }
        }

        return TrustedProxyIp.LeerCabecera(
            context.Request.Headers["CF-Connecting-IP"].ToString());
    }

    private static bool ClavesCoinciden(string recibida, string esperada)
    {
        var bytesRecibidos = Encoding.UTF8.GetBytes(recibida);
        var bytesEsperados = Encoding.UTF8.GetBytes(esperada);

        return bytesRecibidos.Length == bytesEsperados.Length
            && CryptographicOperations.FixedTimeEquals(bytesRecibidos, bytesEsperados);
    }
}
