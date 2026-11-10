using System.Net;
using Microsoft.Extensions.Configuration;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Resuelve la IP del cliente sin confiar en cualquier cabecera enviada por Internet.
///
/// Render termina el TLS en su proxy y reenvia al contenedor desde una IP privada.
/// Cloudflare, que esta delante de Render, escribe la IP real en CF-Connecting-IP.
/// Por eso solo se acepta esa cabecera cuando la conexion inmediata viene de una red
/// privada de Render o de loopback. X-Forwarded-For no se usa para identificar al
/// cliente porque el primer valor lo puede falsificar el propio cliente.
///
/// Las redes de confianza se pueden restringir con Proxy:TrustedNetworks (CIDR separados
/// por coma). Si no se configura, se aceptan las redes privadas IPv4, las ULA/link-local
/// IPv6 y loopback, que es el minimo necesario para Render y desarrollo local.
/// </summary>
internal static class TrustedProxyIp
{
    private const string TrustedNetworksConfigKey = "Proxy:TrustedNetworks";

    private static readonly IPNetwork[] RedesPorDefecto =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10")
    ];

    // Direcciones que nunca son una IP publica de cliente. Si una cabecera trae una de
    // estas, se ignora y se usa la conexion inmediata.
    private static readonly IPNetwork[] RedesNoPublicas =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4"),
        IPNetwork.Parse("255.255.255.255/32"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("ff00::/8")
    ];

    public static IPNetwork[] LeerRedesConfianza(IConfiguration configuration)
    {
        var valor = configuration[TrustedNetworksConfigKey]?.Trim();

        if (string.IsNullOrWhiteSpace(valor))
        {
            return RedesPorDefecto;
        }

        var partes = valor.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var redes = new List<IPNetwork>(partes.Length);

        foreach (var parte in partes)
        {
            try
            {
                redes.Add(IPNetwork.Parse(parte));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    $"Proxy:TrustedNetworks contiene un CIDR invalido: '{parte}'.");
            }
        }

        return redes.Count == 0 ? RedesPorDefecto : redes.ToArray();
    }

    public static IPAddress? Normalizar(IPAddress? direccion)
    {
        if (direccion is null)
        {
            return null;
        }

        return direccion.IsIPv4MappedToIPv6 ? direccion.MapToIPv4() : direccion;
    }

    public static bool EsProxyConfiable(
        IPAddress? direccion,
        IReadOnlyCollection<IPNetwork>? redesConfianza = null)
    {
        var normalizada = Normalizar(direccion);

        if (normalizada is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(normalizada))
        {
            return true;
        }

        var redes = redesConfianza ?? RedesPorDefecto;

        return redes.Any(red => red.Contains(normalizada));
    }

    public static IPAddress? LeerCabecera(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var primerValor = valor.Split(',', 2, StringSplitOptions.TrimEntries)[0];

        if (!IPAddress.TryParse(primerValor, out var direccion))
        {
            return null;
        }

        var normalizada = Normalizar(direccion);

        return normalizada is not null && EsDireccionClienteValida(normalizada)
            ? normalizada
            : null;
    }

    public static IPAddress? ResolverCliente(
        HttpContext contexto,
        IReadOnlyCollection<IPNetwork>? redesConfianza = null)
    {
        var conexion = Normalizar(contexto.Connection.RemoteIpAddress);

        if (!EsProxyConfiable(conexion, redesConfianza))
        {
            return conexion;
        }

        return LeerCabecera(contexto.Request.Headers["CF-Connecting-IP"].ToString())
            ?? conexion;
    }

    private static bool EsDireccionClienteValida(IPAddress direccion)
    {
        if (IPAddress.IsLoopback(direccion))
        {
            return false;
        }

        if (direccion.Equals(IPAddress.Any) ||
            direccion.Equals(IPAddress.IPv6Any) ||
            direccion.IsIPv6LinkLocal ||
            direccion.IsIPv6Multicast)
        {
            return false;
        }

        return !RedesNoPublicas.Any(red => red.Contains(direccion));
    }
}
