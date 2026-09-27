using Rescauta.Domain.Enums;

namespace Rescauta.Application.Dtos;

/// <summary>
/// Proyeccion de <c>Comedor</c> para la API. No expone la entidad: si se devolviera
/// la entidad, cualquier campo nuevo apareceria en el JSON sin que nadie lo revisara.
///
/// <see cref="Estado"/> viaja como string ("Emergencia"), no como numero, porque es lo
/// que consume el frontend para pintar el pin del mapa.
/// </summary>
public sealed record ComedorDto
{
    public required Guid Id { get; init; }

    public required string Nombre { get; init; }

    public required string Distrito { get; init; }

    public required string Direccion { get; init; }

    public string? ContactoDirecto { get; init; }

    public required int RacionesDiarias { get; init; }

    public required EstadoAbastecimiento Estado { get; init; }

    /// <summary>Latitud del punto, o null si el comedor aun no fue geocodificado.</summary>
    public double? Latitud { get; init; }

    public double? Longitud { get; init; }
}
