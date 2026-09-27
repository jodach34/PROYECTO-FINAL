using Rescauta.Application.Features.Kardex.Dto;

namespace Rescauta.Application.Interfaces.RealTime;

/// <summary>
/// Contrato de notificaciones en tiempo real del modulo de Inventario/Kardex.
/// La implementacion que reenvia a SignalR estara en
/// Infrastructure/RealTime/InventoryNotifier.cs.
///
/// Por que existe y por que no se reusa <see cref="IRescautaNotifier"/>: aquel es el
/// transporte generico (recibe un <c>object? payload</c> sin tipar y un nombre de evento
/// libre). Este contrato es tipado y lleva embebidos los nombres de evento del modulo, de
/// modo que un cambio en la forma del payload se rompe al compilar en vez de fallar en
/// runtime con un <c>object</c> serializado vacio.
///
/// Un caso de uso NUNCA inyecta <c>IHubContext&lt;RescautaHub&gt;</c>: inyecta esto.
/// Asi Application no depende de ASP.NET Core.
///
/// Prefijos de evento fijados por Features/Kardex/README.md: <c>kardex.</c> de servidor a
/// cliente. Los nombres viven aqui como constantes y no como literales en el servicio, para
/// que el cliente y el servidor no se desincronicen.
/// </summary>
public interface IInventoryNotifier
{
    /// <summary>
    /// Emite <c>kardex.stock.actualizado</c> con el nuevo saldo de un insumo.
    /// Se llama DESPUES de confirmar la transaccion, nunca antes: si se emitiera antes y la
    /// escritura fallara, los clientes verian un saldo que la base de datos nunca confirmo.
    /// </summary>
    Task NotificarStockActualizadoAsync(
        StockActualizadoDto actualizacion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emite <c>kardex.alerta.emergencia</c> para una situacion critica: ruptura de stock,
    /// perdida de cadena de frio, o cualquier aviso que el operador deba ver ya.
    ///
    /// A diferencia de la actualizacion de stock, esta si se envia al grupo de turno: el
    /// personal en guardia necesita enterarse aunque no tenga abierta la pantalla de kardex.
    /// </summary>
    Task EnviarAlertaEmergenciaAsync(
        AlertaEmergenciaDto alerta,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Nombres de evento del modulo Kardex. Prefijo <c>kardex.</c> (regla del modulo).
/// </summary>
public static class KardexEventos
{
    /// <summary>
    /// Saldo de un insumo cambiado. Servidor -&gt; cliente.
    /// Prefijo de modulo delante, como pide la convencion del Hub compartido.
    /// </summary>
    public const string RecibirActualizacionInventario = "Kardex_RecibirActualizacionInventario";

    /// <summary>
    /// Aviso critico para el grupo de guardia. Servidor -&gt; cliente.
    /// Solo lo recibe quien invoco <c>Kardex_UnirseAlTurno</c> en el hub.
    /// </summary>
    public const string RecibirAlertaEmergencia = "Kardex_RecibirAlertaEmergencia";
}
