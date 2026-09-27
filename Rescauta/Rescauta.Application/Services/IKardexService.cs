using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;

namespace Rescauta.Application.Services;

/// <summary>
/// Caso de uso del kardex: registra entradas, salidas y ajustes de inventario dejando
/// rastro. Es el unico lugar del sistema autorizado a mover existencias.
///
/// POR QUE ESTA EN APPLICATION Y NO EN EL CONTROLLER: el inventario tiene una
/// invariante que no se puede respetar desde la capa de presentacion. Un movimiento
/// son DOS escrituras en la misma tabla logica (el stock del insumo y el renglon del
/// kardex) y si se separan, el historial deja de cuadrar con el stock. Ademas el
/// controller no debe conocer el orden de las escrituras ni decidir cuando se avisa a
/// los susCRIPTores de SignalR.
///
/// REGLA DE ORO: un unico <c>SaveChangesAsync</c> por operacion. EF Core envuelve ese
/// Save en una transaccion, asi que el stock y el movimiento se confirman juntos o se
/// revierten juntos. Si el repositorio guardara por su cuenta, el movimiento quedaria
/// escrito antes de que el stock se actualizara y el inventario quedaria desfasado sin
/// que ninguna operacion fallara.
/// </summary>
public interface IKardexService
{
    /// <summary>Suma existencias: ingreso de mercaderia al almacen del comedor.</summary>
    Task<KardexRegistroResult> RegistrarEntradaAsync(
        Guid insumoId,
        decimal cantidad,
        string? referencia = null,
        string? responsable = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resta existencias por consumo. Falla con <c>DomainException</c> si no hay stock
    /// suficiente: es preferible rechazar el movimiento a dejar el inventario en
    /// negativo, que despues rompe el calculo de dias restantes.
    /// </summary>
    Task<KardexRegistroResult> RegistrarSalidaAsync(
        Guid insumoId,
        decimal cantidad,
        string? referencia = null,
        string? responsable = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrige el stock tras un conteo fisico. <paramref name="delta"/> va con signo:
    /// positivo si el conteo dio mas de lo que dice el sistema, negativo si dio menos.
    /// </summary>
    Task<KardexRegistroResult> RegistrarAjusteAsync(
        Guid insumoId,
        decimal delta,
        string? referencia = null,
        string? responsable = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de registrar un movimiento: el renglon escrito mas el estado del insumo
/// despues del cambio. Se devuelve el estado recalculado para que el endpoint no tenga
/// que releer el insumo y para que el panel reciba de una vez el dato que va a pintar.
/// </summary>
/// <param name="Movimiento">Renglon de kardex persistido.</param>
/// <param name="InsumoNombre">Nombre del insumo, para el payload de SignalR.</param>
/// <param name="UnidadMedida">Unidad del insumo, idem.</param>
/// <param name="Estado">Estado de abastecimiento recalculado tras el movimiento.</param>
public sealed record KardexRegistroResult(
    MovimientoKardex Movimiento,
    string InsumoNombre,
    string UnidadMedida,
    EstadoAbastecimiento Estado);
