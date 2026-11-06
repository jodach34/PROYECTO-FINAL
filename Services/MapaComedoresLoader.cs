using PROYECTO_FINAL.Models;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Arma el modelo del mapa interactivo reutilizando los endpoints publicos de comedores.
///
/// MapaX/MapaY son porcentajes 0-100 sobre el lienzo esquematico, no coordenadas GPS. Para
/// que Leaflet reciba latitud y longitud validas se proyectan linealmente sobre un recuadro
/// de Lima Metropolitana. No se guardan ni se inventan datos: solo se transforma la
/// posicion que ya tenia cada comedor.
/// </summary>
public static class MapaComedoresLoader
{
    private const double LatitudNorte = -11.78;
    private const double LatitudSur = -12.58;
    private const double LongitudOeste = -77.18;
    private const double LongitudEste = -76.60;

    public static async Task<MapaViewModel> CargarAsync(
        RescautaApiClient api,
        CancellationToken cancellationToken)
    {
        var resumen = await api.ObtenerComedoresAsync(cancellationToken);

        if (!resumen.Exito)
        {
            return new MapaViewModel { Error = resumen.Mensaje };
        }

        var comedores = resumen.Valor!;

        // El resumen no trae la direccion, asi que se reutiliza el endpoint de detalle.
        // Si un detalle falla, el comedor sigue en el mapa y solo se omite su direccion.
        var detalles = await Task.WhenAll(
            comedores.Select(c => api.ObtenerComedorAsync(c.Id, cancellationToken)));

        var detallesPorId = detalles
            .Where(d => d.Exito)
            .Select(d => d.Valor!)
            .ToDictionary(d => d.Id);

        var puntos = new List<ComedorMapaPunto>(comedores.Count);

        foreach (var comedor in comedores)
        {
            if (!TryProyectar(comedor.MapaX, comedor.MapaY, out var latitud, out var longitud))
            {
                continue;
            }

            detallesPorId.TryGetValue(comedor.Id, out var detalle);

            puntos.Add(new ComedorMapaPunto
            {
                Id = comedor.Id,
                Nombre = comedor.Nombre,
                Direccion = detalle?.Direccion ?? string.Empty,
                Distrito = comedor.Distrito,
                Estado = comedor.Estado,
                Latitud = latitud,
                Longitud = longitud
            });
        }

        return new MapaViewModel
        {
            Comedores = comedores,
            PuntosMapa = puntos
        };
    }

    private static bool TryProyectar(
        decimal mapaX,
        decimal mapaY,
        out double latitud,
        out double longitud)
    {
        latitud = 0;
        longitud = 0;

        if (mapaX is < 0m or > 100m || mapaY is < 0m or > 100m)
        {
            return false;
        }

        longitud = LongitudOeste + ((double)mapaX / 100d * (LongitudEste - LongitudOeste));
        latitud = LatitudNorte + ((double)mapaY / 100d * (LatitudSur - LatitudNorte));

        return true;
    }
}
