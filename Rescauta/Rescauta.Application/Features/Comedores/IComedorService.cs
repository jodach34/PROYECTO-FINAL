using Rescauta.Application.Common;
using Rescauta.Application.Features.Comedores.Dto;
using Rescauta.Application.Features.Kardex;

namespace Rescauta.Application.Features.Comedores;

/// <summary>
/// Casos de uso del mapa de comedores.
///
/// El modulo Comedores solo escribe para dar de alta un comedor. Lo que cambia el inventario
/// es el modulo Kardex (RegistrarMovimientoAsync) y lo que registra una donacion es el de
/// Donaciones. Esta interfaz existe para que el mapa, el panel y el alta tengan una unica
/// puerta de entrada a esos datos, en vez de que cada controller arrastre su propio LINQ
/// contra <c>IAppDbContext</c>.
/// </summary>
public interface IComedorService
{
    /// <summary>
    /// Registra un comedor nuevo. El id, el estado de abastecimiento y el inventario se
    /// derivan de la entidad; el cliente solo manda los datos de catalogo.
    /// </summary>
    Task<Result<ComedorResultDto>> CrearAsync(
        CrearComedorDto comedor,
        CancellationToken cancellationToken = default);

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
    ///
    /// Solo el Administrador puede consultar cualquier comedor. Operador y Coordinador
    /// quedan limitados a los comedores que tienen asignados.
    /// </summary>
    Task<Result<IReadOnlyList<KardexFilaDto>>> ObtenerKardexAsync(
        Guid comedorId,
        UsuarioOperacion usuario,
        int limite = 20,
        CancellationToken cancellationToken = default);
}
