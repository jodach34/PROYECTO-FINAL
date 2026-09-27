using Rescauta.Domain.Enums;

namespace Rescauta.Application.Dtos;

/// <summary>
/// Proyeccion de <c>Donacion</c> para la API.
///
/// Incluye <see cref="QrDataUri"/> porque el frontend del Banco de Alimentos muestra
/// el QR y el codigo juntos en la misma tarjeta de confirmacion: el servidor lo
/// resuelve, el cliente no.
/// </summary>
public sealed record DonacionDto
{
    public required Guid Id { get; init; }

    /// <summary>Codigo legible que viaja en el QR. Ej.: "DON-2026-00421".</summary>
    public required string CodigoSeguimiento { get; init; }

    public required string NombreProducto { get; init; }

    public required string Categoria { get; init; }

    public required decimal Cantidad { get; init; }

    public required string UnidadMedida { get; init; }

    public DateOnly? CaducidadAprox { get; init; }

    public required string DireccionRecojo { get; init; }

    public required EstadoDonacion Estado { get; init; }

    public Guid? ComedorAsignadoId { get; init; }

    /// <summary>Nombre del comedor asignado, para mostrarlo sin una segunda llamada.</summary>
    public string? ComedorAsignadoNombre { get; init; }

    /// <summary>Data URI del QR. Null hasta que la donacion tenga codigo asignado.</summary>
    public string? QrDataUri { get; init; }
}

/// <summary>
/// Comedor que el sistema recomienda para una donacion, con lo que el frontend
/// necesita para pintar la tabla de urgencia sin calcular nada.
/// </summary>
public sealed record ComedorRecomendadoDto
{
    public required Guid Id { get; init; }

    public required string Nombre { get; init; }

    public required string Distrito { get; init; }

    public required EstadoAbastecimiento Urgencia { get; init; }

    /// <summary>Distancia en km desde el punto de recojo del donante.</summary>
    public required double DistanciaKm { get; init; }

    public required int RacionesDiarias { get; init; }
}
