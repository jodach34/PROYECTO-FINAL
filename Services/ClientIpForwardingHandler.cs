namespace PROYECTO_FINAL.Services;

/// <summary>
/// Envia a la API la IP real del navegador y la clave interna del MVC. La API solo
/// acepta esa IP si la clave coincide, de modo que un cliente que llame a la API
/// directamente no pueda falsificar su propia IP.
/// </summary>
public sealed class ClientIpForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IHostEnvironment environment) : DelegatingHandler
{
    public const string ClientIpHeader = "X-Rescauta-Client-Ip";
    public const string InternalKeyHeader = "X-Rescauta-Internal-Key";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var contexto = httpContextAccessor.HttpContext;
        var ip = contexto?.Connection.RemoteIpAddress;

        if (ip is not null)
        {
            request.Headers.TryAddWithoutValidation(ClientIpHeader, ip.ToString());
        }

        var claveInterna = configuration["Internal:ApiKey"];

        if (!string.IsNullOrWhiteSpace(claveInterna))
        {
            if (environment.IsProduction() &&
                (request.RequestUri is null ||
                 request.RequestUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    "No se envia la clave interna a la API por HTTP. Api:BaseUrl debe usar https en produccion.");
            }

            request.Headers.TryAddWithoutValidation(InternalKeyHeader, claveInterna);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
