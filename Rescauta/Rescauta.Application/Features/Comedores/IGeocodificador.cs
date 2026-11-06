namespace Rescauta.Application.Features.Comedores;

/// <summary>Coordenadas GPS devueltas por un geocodificador.</summary>
public readonly record struct CoordenadasGeograficas(decimal Latitud, decimal Longitud);

/// <summary>
/// Traduce una direccion postal a coordenadas GPS. La implementacion concreta vive en
/// Infrastructure para que Application no dependa de HTTP ni de Nominatim.
/// </summary>
public interface IGeocodificador
{
    /// <summary>
    /// Falso cuando la geocodificacion esta deshabilitada por configuracion. En ese caso no
    /// se debe marcar el intento como fallido: la direccion puede geocodificarse mas adelante.
    /// </summary>
    bool EstaHabilitado { get; }

    /// <summary>
    /// Devuelve null cuando la direccion no se pudo ubicar o el proveedor no respondio.
    /// No lanza por errores de red: el alta del comedor no debe fallar por un geocodificador
    /// externo caido.
    /// </summary>
    Task<CoordenadasGeograficas?> GeocodificarAsync(
        string direccion,
        string distrito,
        CancellationToken cancellationToken = default);
}
