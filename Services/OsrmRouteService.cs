using System.Text.Json;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Cliente del servicio publico de OSRM. No agrega dependencias: usa el HttpClient
/// tipado que registra Program.cs y System.Text.Json para leer la respuesta.
///
/// OSRM devuelve las coordenadas como [longitud, latitud]; aqui se convierten a
/// [latitud, longitud] para que el JavaScript pueda pasarlas directo a L.polyline.
/// Todos los fallos se traducen a Result.Failure: la vista nunca recibe una excepcion
/// por culpa del servicio de rutas.
/// </summary>
public sealed class OsrmRouteService(HttpClient httpClient, ILogger<OsrmRouteService> logger)
{
    public async Task<Result<RutaOsrm>> CalcularRutaAsync(
        double origenLat,
        double origenLng,
        double destinoLat,
        double destinoLng,
        CancellationToken cancellationToken = default)
    {
        // InvariantCulture es obligatorio: en un servidor con configuracion regional
        // espanola, un decimal con coma rompe la URL de OSRM.
        var ruta = FormattableString.Invariant(
            $"route/v1/driving/{origenLng:F6},{origenLat:F6};{destinoLng:F6},{destinoLat:F6}?overview=full&geometries=geojson&steps=false");

        try
        {
            using var respuesta = await httpClient.GetAsync(ruta, cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "OSRM respondio HTTP {Status} al calcular la ruta.",
                    (int)respuesta.StatusCode);

                return Result<RutaOsrm>.Failure(
                    "osrm_no_disponible",
                    "El servicio de rutas no respondio correctamente.");
            }

            await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(
                flujo,
                cancellationToken: cancellationToken);

            var raiz = documento.RootElement;

            var codigoTexto = raiz.TryGetProperty("code", out var codigo) &&
                              codigo.ValueKind == JsonValueKind.String
                ? codigo.GetString()
                : null;

            if (codigoTexto != "Ok")
            {
                logger.LogInformation("OSRM no encontro ruta. Codigo={Codigo}.", codigoTexto);

                return Result<RutaOsrm>.Failure(
                    "osrm_sin_ruta",
                    "No se encontro una ruta por carretera entre el origen y el comedor.");
            }

            if (!raiz.TryGetProperty("routes", out var rutas) || rutas.GetArrayLength() == 0)
            {
                return Result<RutaOsrm>.Failure(
                    "osrm_sin_ruta",
                    "El servicio de rutas no devolvio ninguna ruta.");
            }

            var primera = rutas[0];
            var distancia = primera.TryGetProperty("distance", out var distanciaElemento)
                ? distanciaElemento.GetDouble()
                : 0d;
            var duracion = primera.TryGetProperty("duration", out var duracionElemento)
                ? duracionElemento.GetDouble()
                : 0d;
            var geometria = new List<double[]>();

            if (primera.TryGetProperty("geometry", out var geo) &&
                geo.TryGetProperty("coordinates", out var coordenadas))
            {
                foreach (var punto in coordenadas.EnumerateArray())
                {
                    if (punto.GetArrayLength() >= 2)
                    {
                        geometria.Add([punto[1].GetDouble(), punto[0].GetDouble()]);
                    }
                }
            }

            return Result<RutaOsrm>.Success(
                new RutaOsrm(distancia, duracion, geometria));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "No se pudo calcular la ruta con OSRM.");

            return Result<RutaOsrm>.Failure(
                "osrm_no_disponible",
                "No se pudo calcular la ruta en este momento. El registro de la donacion no se ve afectado.");
        }
    }
}

/// <summary>Ruta tal como la devuelve OSRM, ya normalizada.</summary>
public sealed record RutaOsrm(
    double DistanciaMetros,
    double DuracionSegundos,
    IReadOnlyList<double[]> Geometria);
