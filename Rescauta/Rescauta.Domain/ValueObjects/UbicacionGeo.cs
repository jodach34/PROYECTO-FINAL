using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.ValueObjects;

/// <summary>
/// Coordenada geografica de un punto de reparto.
///
/// Es un value object y no una entidad: no tiene identidad propia, dos coordenadas
/// con los mismos numeros son el mismo valor. La igualdad la resuelve la clase base
/// <see cref="ValueObject"/> comparando componentes.
///
/// EF Core no sabe mapearlo solo: la conversion a dos columnas (Latitud, Longitud)
/// vive en Infrastructure/Persistence/Configurations/ComedorConfiguration.cs, para que
/// el Domain siga sin dependencias de EF Core.
/// </summary>
public sealed class UbicacionGeo : ValueObject
{
    public double Latitud { get; }

    public double Longitud { get; }

    public UbicacionGeo(double latitud, double longitud)
    {
        if (double.IsNaN(latitud) || latitud is < -90 or > 90)
        {
            throw new DomainException(
                $"La latitud debe estar entre -90 y 90. Recibido: {latitud}.");
        }

        if (double.IsNaN(longitud) || longitud is < -180 or > 180)
        {
            throw new DomainException(
                $"La longitud debe estar entre -180 y 180. Recibido: {longitud}.");
        }

        Latitud = Math.Round(latitud, 6);
        Longitud = Math.Round(longitud, 6);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitud;
        yield return Longitud;
    }

    /// <summary>Formato "latitud,longitud", el que aceptan los servicios de geocodificacion.</summary>
    public override string ToString() => $"{Latitud:F6},{Longitud:F6}";
}
