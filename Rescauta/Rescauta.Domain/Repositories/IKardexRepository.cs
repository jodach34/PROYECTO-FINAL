using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;

namespace Rescauta.Domain.Repositories;

/// <summary>
/// Contrato de acceso a datos del kardex: insumos y sus movimientos.
///
/// El punto importante es que <see cref="ListarMovimientosPorInsumoAsync"/> promete
/// orden cronologico. El kardex es append-only y la auditoria depende de que el
/// historial salga siempre en el mismo orden, asi que ese ORDER BY es parte del
/// contrato, no un detalle de la implementacion.
/// </summary>
public interface IKardexRepository
{
    Task<Insumo?> ObtenerInsumoAsync(Guid insumoId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Insumo>> ListarInsumosPorComedorAsync(
        Guid comedorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Insumo>> ListarInsumosCriticosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Historial de un insumo, del movimiento mas antiguo al mas reciente.</summary>
    Task<IReadOnlyList<MovimientoKardex>> ListarMovimientosPorInsumoAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default);

    /// <summary>Ultimos movimientos de todo el sistema, para la tabla del panel.</summary>
    Task<IReadOnlyList<MovimientoKardex>> ListarMovimientosRecientesAsync(
        int cantidad = 20,
        CancellationToken cancellationToken = default);

    Task AgregarInsumoAsync(Insumo insumo, CancellationToken cancellationToken = default);

    Task AgregarMovimientoAsync(MovimientoKardex movimiento, CancellationToken cancellationToken = default);

    void ActualizarInsumo(Insumo insumo);

    /// <summary>
    /// Calcula el stock del insumo a partir de todos sus movimientos. Es la unica via
    /// admitida para obtener el saldo: tomar StockActual como verdad permitiria que un
    /// movimiento se perdiera sin que nadie lo notara.
    /// </summary>
    Task<decimal> CalcularStockAsync(Guid insumoId, CancellationToken cancellationToken = default);

    Task<EstadoAbastecimiento> CalcularEstadoAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default);
}
