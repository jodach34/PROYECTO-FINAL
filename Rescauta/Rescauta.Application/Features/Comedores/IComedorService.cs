using Rescauta.Application.Common;
using Rescauta.Application.Features.Comedores.Dto;

namespace Rescauta.Application.Features.Comedores;

/// <summary>
/// Casos de uso de lectura del mapa de comedores.
///
/// Todos son de solo lectura: el modulo Comedores no escribe en el dominio. Lo que cambia el
/// inventario es el modulo Kardex (RegistrarMovimientoAsync) y lo que registra una ~
/// donacion es el de Donaciones. Esta interfaz existe para que el mapa y el panel tengan una
/// unica puerta de entrada a esos datos, en vez de que cada controller arraste su propio
/// LINQ contra <c>IAppDbContext</c>.
/// </summary>
public interface IComedorService
{
    /// <summary>
    /// Lista los comedores con su estado de abastecimiento, del mas urgente al menos
    /// urgente. Es lo que pintan el mapa y el sidebar.
    /// </summary>
    Task<Result<IReadOnlyList<ComedorResumenDto>>> ObtenerResumenAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Ficha completa de un comedor, con su inventario insumo por insumo.</summary>
    Task<Result<ComedorDetalleDto>> ObtenerDetalleAsync(
        Guid comedorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kardex de un comedor: los movimientos de todos sus insumos, del mas reciente al mas
    /// antiguo. Es la tabla del Panel de Control.
    /// </summary>
    Task<Result<IReadOnlyList<KardexFilaDto>>> ObtenerKardexAsync(
        Guid comedorId,
        int limite = 20,
        CancellationToken cancellationToken = default);
}
