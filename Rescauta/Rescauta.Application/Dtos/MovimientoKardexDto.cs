using Rescauta.Domain.Enums;

namespace Rescauta.Application.Dtos;

/// <summary>
/// Proyeccion de un renglon del kardex para la tabla "Registro de Movimientos".
///
/// <see cref="Tipo"/> y <see cref="CantidadConSigno"/> llegan separados a proposito: el
/// frontend pinta "+ 12 Litros" en verde y "- 4.5 kg" en rojo usando el signo, sin
/// tener que parsear el numero.
/// </summary>
public sealed record MovimientoKardexDto
{
    public required Guid Id { get; init; }

    public required Guid InsumoId { get; init; }

    /// <summary>Nombre del insumo, resuelto para que la tabla no haga un N+1.</summary>
    public required string InsumoNombre { get; init; }

    public required TipoMovimientoKardex Tipo { get; init; }

    /// <summary>Cantidad siempre positiva.</summary>
    public required decimal Cantidad { get; init; }

    /// <summary>Cantidad con signo: negativa si el movimiento fue una salida.</summary>
    public required decimal CantidadConSigno { get; init; }

    public string UnidadMedida { get; init; } = string.Empty;

    public required decimal StockResultante { get; init; }

    public required DateTimeOffset Fecha { get; init; }

    public string? Referencia { get; init; }

    public string? Responsable { get; init; }
}
