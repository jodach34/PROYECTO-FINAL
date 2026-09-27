using Rescauta.Domain.Common;

namespace Rescauta.Domain.Events;

/// <summary>
/// Evento de dominio que emite <c>Donacion.AsignarAComedor</c>.
///
/// Es el unico contrato entre Donaciones y Kardex. Un handler de Application lo
/// escucha y escribe el movimiento de inventario correspondiente, de modo que el
/// modulo de Donaciones nunca importa entidades ni repositorios del de Kardex.
///
/// Transporta solo ids y datos primitivos, nunca entidades completas.
/// </summary>
public sealed record DonacionConfirmada : DomainEvent
{
    public required Guid DonacionId { get; init; }

    public required Guid ComedorId { get; init; }

    /// <summary>Codigo legible de la donacion, para dejar trazabilidad en el kardex.</summary>
    public required string CodigoSeguimiento { get; init; }

    public required string NombreProducto { get; init; }

    public required decimal Cantidad { get; init; }

    public required string UnidadMedida { get; init; }
}
