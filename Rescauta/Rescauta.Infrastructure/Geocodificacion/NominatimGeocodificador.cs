using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Comedores;
using Rescauta.Infrastructure.Options;

namespace Rescauta.Infrastructure.Geocodificacion;

/// <summary>
/// Geocodificador basado en el endpoint publico de OpenStreetMap Nominatim.
///
/// Reglas de uso que implementa esta clase:
///   * Un User-Agent identificable en cada peticion.
///   * Una sola consulta cada 1000 ms como minimo, compartida por toda la aplicacion.
///   * Si Nominatim falla, devuelve null; no propaga el error al alta del comedor.
///   * El resultado se persiste en el comedor, asi no se repite la consulta en cada arranque.
/// </summary>
public sealed class NominatimGeocodificador(
    IHttpClientFactory httpClientFactory,
    IOptions<GeocodificacionOptions> options,
    ILogger<NominatimGeocodificador> logger) : IGeocodificador
{
    public const string HttpClientName = "nominatim";

    private readonly GeocodificacionOptions _options = options.Value;
    private readonly SemaphoreSlim _turno = new(1, 1);
    private DateTimeOffset _ultimaConsulta = DateTimeOffset.MinValue;

    public bool EstaHabilitado => _options.Enabled;

    public async Task<CoordenadasGeograficas?> GeocodificarAsync(
        string direccion,
        string distrito,
        CancellationToken cancellationToken = default)
    {
        if (!EstaHabilitado || string.IsNullOrWhiteSpace(direccion))
        {
            return null;
        }

        var consulta = ConstruirConsulta(direccion, distrito);

        await _turno.WaitAsync(cancellationToken);

        try
        {
            var intervalo = TimeSpan.FromMilliseconds(
                Math.Max(1000, _options.IntervaloEntreConsultasMs));
            var espera = _ultimaConsulta + intervalo - DateTimeOffset.UtcNow;

            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, cancellationToken);
            }

            _ultimaConsulta = DateTimeOffset.UtcNow;

            return await ConsultarAsync(consulta, cancellationToken);
        }
        finally
        {
            _turno.Release();
        }
    }

    private async Task<CoordenadasGeograficas?> ConsultarAsync(
        string consulta,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/search" +
            $"?format=jsonv2&limit=1&countrycodes=pe" +
            $"&accept-language={Uri.EscapeDataString(_options.Idioma)}" +
            $"&q={Uri.EscapeDataString(consulta)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", _options.Idioma);

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Nominatim respondio {Status} para la direccion {Direccion}.",
                    (int)response.StatusCode,
                    consulta);

                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0)
            {
                logger.LogInformation(
                    "Nominatim no encontro coordenadas para la direccion {Direccion}.",
                    consulta);

                return null;
            }

            var resultado = document.RootElement[0];

            if (!resultado.TryGetProperty("lat", out var latitudElemento) ||
                !resultado.TryGetProperty("lon", out var longitudElemento) ||
                !decimal.TryParse(
                    latitudElemento.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitud) ||
                !decimal.TryParse(
                    longitudElemento.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitud))
            {
                logger.LogWarning(
                    "Nominatim devolvio coordenadas invalidas para la direccion {Direccion}.",
                    consulta);

                return null;
            }

            if (latitud is < -90m or > 90m || longitud is < -180m or > 180m)
            {
                logger.LogWarning(
                    "Nominatim devolvio coordenadas fuera de rango para la direccion {Direccion}.",
                    consulta);

                return null;
            }

            return new CoordenadasGeograficas(latitud, longitud);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo consultar Nominatim para la direccion {Direccion}.",
                consulta);

            return null;
        }
    }

    private string ConstruirConsulta(string direccion, string distrito)
    {
        var partes = new[] { direccion, distrito, _options.Pais }
            .Where(parte => !string.IsNullOrWhiteSpace(parte))
            .Select(parte => parte.Trim());

        return string.Join(", ", partes);
    }
}
