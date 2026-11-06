using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Features.Kardex.Dto;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Infrastructure.Hubs;

namespace Rescauta.Infrastructure.RealTime;

/// <summary>
/// Implementacion SignalR de <see cref="IInventoryNotifier"/>. Es el unico punto del sistema
/// que conoce a la vez Application y el Hub, por eso vive en Infrastructure y no en
/// Application.
///
/// USA EL HUB COMPARTIDO <see cref="RescautaHub"/>, no un hub propio del modulo. Es lo que
/// manda la convencion del proyecto (ver RescautaHub y Program.cs, seccion 10): un solo hub,
/// una sola conexion WebSocket por cliente. Un InventoryHub aparte habria abierto un segundo
/// socket en el navegador y duplicado los eventos de stock en el panel.
///
/// Se diferencia de <see cref="RescautaRealtimeNotifier"/> en que NO recibe el nombre del
/// evento como parametro. <see cref="IInventoryNotifier"/> expone metodos tipados, asi que el
/// nombre queda atado a una constante: ni el servicio de Application ni esta clase pueden
/// escribir un nombre equivocado, y un typo no rompe el canal en silencio.
///
/// Prefijos (convention del proyecto):
///   * servidor -&gt; cliente: "Kardex_" + accion, con el prefijo de modulo delante.
///   * nombres de evento en <see cref="KardexEventos"/> (Application), que es donde el
///     cliente deberia leerlos.
///
/// REGISTRO EN DI (NO se modifica Infrastructure/DependencyInjection.cs desde este modulo):
/// anadir
///   <c>services.AddSingleton&lt;IInventoryNotifier, SignalRInventoryNotifier&gt;();</c>
/// Queda pendiente porque ese archivo es compartido y lo edita el dev del composition root.
/// Mismo caso que IRescautaNotifier, que ya esta registrado ahi.
///
/// Nota de ciclo de vida: <see cref="IHubContext{THub}"/> es singleton con vida corta por
/// coneccion y seguro de compartir, asi que el notificador va como singleton. Si se
/// registrara scoped, cada peticion crearia una instancia sin ganar nada.
/// </summary>
public sealed class SignalRInventoryNotifier(
    IHubContext<RescautaHub> hubContext,
    ILogger<SignalRInventoryNotifier> logger) : IInventoryNotifier
{
    /// <summary>
    /// Saldo de un insumo cambiado. Se difunde a todos los clientes: cualquiera con el
    /// panel de inventario abierto necesita el dato al instante, y el volume es bajo (un
    /// evento por movimiento, no por lectura).
    /// </summary>
    public async Task NotificarStockActualizadoAsync(
        StockActualizadoDto actualizacion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actualizacion);

        logger.LogDebug(
            "Difusion de {Evento} para el insumo {InsumoId}. Stock {StockPrevio} -> {StockActual}",
            KardexEventos.RecibirActualizacionInventario,
            actualizacion.InsumoId,
            actualizacion.StockPrevio,
            actualizacion.StockActual);

        await hubContext.Clients.All.SendAsync(
            KardexEventos.RecibirActualizacionInventario,
            actualizacion,
            cancellationToken);
    }

    /// <summary>
    /// Alerta critica. Va solo al grupo de guardia (<see cref="RescautaHub.KardexTurnoGroup"/>),
    /// no a todos: asi el turno se entera sin que se despierte la gente de la oficina.
    ///
    /// Riesgo a tener en cuenta: si el cliente nunca invoco <c>Kardex_UnirseAlTurno</c>, el
    /// grupo esta vacio y la alerta se pierde en silencio. Por eso se loguea SIEMPRE con
    /// LogWarning, haya clientes conectados o no: ese log es la unica traza de que hubo una
    /// ruptura de stock cuando el hub esta caido.
    /// </summary>
    public async Task EnviarAlertaEmergenciaAsync(
        AlertaEmergenciaDto alerta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alerta);

        // Antes de emitir, por si el canal no llega a nadie.
        logger.LogWarning(
            "Alerta de inventario nivel {Nivel}. Insumo={InsumoId} ({Nombre}), Stock={Stock}. {Mensaje}",
            alerta.Nivel,
            alerta.InsumoId,
            alerta.Nombre,
            alerta.StockActual,
            alerta.Mensaje);

        await hubContext.Clients.Group(RescautaHub.KardexTurnoGroup).SendAsync(
            KardexEventos.RecibirAlertaEmergencia,
            alerta,
            cancellationToken);
    }
}
