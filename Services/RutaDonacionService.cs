using PROYECTO_FINAL.Models;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Une las donaciones con OSRM. Recibe una donacion ya leida de la API, resuelve el
/// comedor destino y devuelve un objeto listo para serializar a JSON.
///
/// Si falta el origen o el destino no se llama a OSRM: se devuelve el motivo para que la
/// vista lo explique. Si OSRM falla, se devuelven igual las coordenadas disponibles para
/// que el mapa muestre los puntos, y un mensaje claro de que la ruta no esta disponible.
/// </summary>
public sealed class RutaDonacionService(RescautaApiClient api, OsrmRouteService osrm)
{
    public async Task<RutaDonacionRespuesta> CalcularAsync(
        DonacionRegistrada donacion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(donacion);

        var origen = CrearPunto(
            donacion.LatitudOrigen,
            donacion.LongitudOrigen,
            string.IsNullOrWhiteSpace(donacion.PuntoRecojo)
                ? "Punto de recogida"
                : donacion.PuntoRecojo);

        var destino = await ObtenerDestinoAsync(donacion, cancellationToken);

        if (origen is null)
        {
            return new RutaDonacionRespuesta
            {
                Motivo = "sin_origen",
                Mensaje = "Falta registrar la ubicacion del punto de recogida. " +
                          "Marca el origen en el formulario para poder calcular la ruta.",
                Destino = destino
            };
        }

        if (destino is null)
        {
            return new RutaDonacionRespuesta
            {
                Motivo = "sin_destino",
                Mensaje = "El comedor destino no tiene coordenadas GPS registradas.",
                Origen = origen
            };
        }

        var calculo = await osrm.CalcularRutaAsync(
            origen.Lat,
            origen.Lng,
            destino.Lat,
            destino.Lng,
            cancellationToken);

        if (!calculo.Exito)
        {
            return new RutaDonacionRespuesta
            {
                Motivo = calculo.Codigo,
                Mensaje = calculo.Mensaje ?? "No se pudo calcular la ruta.",
                Origen = origen,
                Destino = destino
            };
        }

        var ruta = calculo.Valor!;

        return new RutaDonacionRespuesta
        {
            Disponible = true,
            Mensaje = "Ruta calculada por carretera con OSRM.",
            DistanciaKm = Math.Round(ruta.DistanciaMetros / 1000d, 1),
            DuracionMinutos = Math.Round(ruta.DuracionSegundos / 60d, 0),
            Origen = origen,
            Destino = destino,
            Geometria = ruta.Geometria
        };
    }

    private async Task<PuntoRuta?> ObtenerDestinoAsync(
        DonacionRegistrada donacion,
        CancellationToken cancellationToken)
    {
        var resultado = await api.ObtenerComedorAsync(donacion.ComedorId, cancellationToken);

        if (!resultado.Exito || resultado.Valor is null)
        {
            return null;
        }

        return CrearPunto(
            resultado.Valor.Latitud,
            resultado.Valor.Longitud,
            resultado.Valor.Nombre);
    }

    private static PuntoRuta? CrearPunto(decimal? latitud, decimal? longitud, string? nombre = null)
    {
        if (latitud is null || longitud is null)
        {
            return null;
        }

        var lat = (double)latitud.Value;
        var lng = (double)longitud.Value;

        if (!double.IsFinite(lat) || !double.IsFinite(lng) ||
            Math.Abs(lat) > 90d || Math.Abs(lng) > 180d ||
            (lat == 0d && lng == 0d))
        {
            return null;
        }

        return new PuntoRuta
        {
            Lat = lat,
            Lng = lng,
            Nombre = nombre ?? string.Empty
        };
    }
}
