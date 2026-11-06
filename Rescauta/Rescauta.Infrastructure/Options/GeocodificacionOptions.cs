namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "Geocodificacion" de appsettings.json.
///
/// Nominatim exige un User-Agent que identifique la aplicacion y un maximo de una consulta
/// por segundo. El intervalo se configura para poder bajarlo en pruebas, pero en uso normal
/// no debe quedar por debajo de 1000 ms.
/// </summary>
public sealed class GeocodificacionOptions
{
    public const string SectionName = "Geocodificacion";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    public string UserAgent { get; set; } = "Rescauta/1.0 (contacto@rescauta.local)";

    public string Pais { get; set; } = "Peru";

    public string Idioma { get; set; } = "es";

    public int TimeoutSeconds { get; set; } = 10;

    public int IntervaloEntreConsultasMs { get; set; } = 1100;

    /// <summary>Cada cuanto vuelve a buscar comedores sin intento de geocodificacion.</summary>
    public int BackfillIntervalMinutes { get; set; } = 5;

    /// <summary>Cuantos comedores procesa como maximo en cada pasada del backfill.</summary>
    public int BackfillLote { get; set; } = 20;
}
