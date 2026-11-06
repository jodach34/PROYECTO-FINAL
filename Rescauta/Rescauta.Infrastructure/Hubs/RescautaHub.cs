using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Rescauta.Infrastructure.Hubs;

/// <summary>
/// Carga util de la prueba de humo del hub. Tipada a proposito: si se devuelve un objeto
/// anonimo, el cliente no puede deserializarlo salvo que declare el tipo exacto y la
/// prueba se vuelve fragil.
/// </summary>
public sealed record RescautaUpdate(
    string Message,
    string? ConnectionId,
    string? Group,
    DateTimeOffset SentAt);

/// <summary>
/// Hub base de tiempo real de Rescauta. Se registra y rutea en Rescauta.Api/Program.cs
/// (<c>AddSignalR()</c> + <c>MapHub&lt;RescautaHub&gt;("/hubs/rescauta")</c>).
///
/// Convenciones para los 3 modulos:
///   * Eventos cliente -&gt; servidor: un metodo publico por accion, con prefijo de modulo
///     ("Mapas_", "Kardex_", "Donaciones_"). Asi un dev nunca pisa el metodo de otro.
///   * Eventos servidor -&gt; cliente: se emiten con nombre "modulo.entidad.accion", por
///     ejemplo "kardex.movimiento.registrado" o "mapas.ruta.actualizada".
///   * Los metodos que envian eventos devuelven <c>Task</c> y lo esperan con <c>await</c>.
///     Nunca devolver el resultado como <c>object</c>: SignalR lo toma comopayload de la
///     invocacion y falla al serializarlo.
///   * RescautaUpdate es la carga de la prueba de humo: si el dev la recibe por WebSocket,
///     el cableado de SignalR esta bien en su maquina.
///
/// Modulo Kardex (dev 2) - eventos anadidos:
///   * <c>Kardex_UnirseAlTurno</c> / <c>Kardex_SalirDelTurno</c>: gestionan el grupo de
///     guardia, que es quien recibe las alertas de emergencia. Los eventos de inventario
///     llegan por el mismo hub con prefijo de modulo, asi que no colisionan con los de
///     Mapas ni Donaciones.
///   * El grupo es KardexTurnoGroup y el notificador es SignalRInventoryNotifier, que
///     implementa IInventoryNotifier con este mismo IHubContext. No hay un hub por modulo:
///     una sola conexion WebSocket por cliente, no una por cada modulo abierto.
///
/// Autenticacion: [AllowAnonymous] esta a proposito para el cascarón. El equipo debe
/// decidir el mecanismo (JWT) y cambiarlo antes de exponer el hub.
/// </summary>
[AllowAnonymous]
public class RescautaHub : Hub
{
    public const string Route = "/hubs/rescauta";

    /// <summary>Prueba de humo del canal WebSocket: hace eco al cliente que invoco.</summary>
    public Task SendUpdate(string message) => Clients.Caller.SendAsync(
        "updateReceived",
        new RescautaUpdate(message, Context.ConnectionId, null, DateTimeOffset.UtcNow));

    /// <summary>Prueba de emision a un grupo, por ejemplo una ruta de reparto.</summary>
    public Task SendUpdateToGroup(string groupName, string message) => Clients.Group(groupName).SendAsync(
        "updateReceived",
        new RescautaUpdate(message, null, groupName, DateTimeOffset.UtcNow));

    // ---------------------------------------------------------------------------------
    // MODULO KARDEX (dev 2) - grupos de guardia
    //
    // Las alertas de emergencia no se difunden a todos: van solo al personal de turno. Un
    // operador se une al grupo al abrir su puesto y lo abandona al cerrarlo. Sin este par
    // de metodos, SignalRInventoryNotifier emitiria a un grupo vacio y la alerta se
    // perderia en silencio.
    //
    // El nombre del grupo NO lleva el prefijo "Kardex_" porque no es un metodo: es un
    // destino de difusion. Comparte el espacio de nombres con los grupos de los otros
    // modulos, asi que se antepone el modulo.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Grupo de guardia del modulo Kardex. Destino de las alertas de emergencia.
    /// </summary>
    public const string KardexTurnoGroup = "kardex.turno";

    /// <summary>
    /// Une la conexion al grupo de guardia para recibir alertas de emergencia.
    /// El cliente lo invoca al iniciar sesion en un puesto de operador.
    /// </summary>
    public Task Kardex_UnirseAlTurno() => Groups.AddToGroupAsync(Context.ConnectionId, KardexTurnoGroup);

    /// <summary>
    /// Sale del grupo de guardia. Se invoca al cerrar sesion o al cambiar de turno: sin
    /// esto, un socket abierto seguiria recibiendo alertas de un turno ya terminado.
    /// </summary>
    public Task Kardex_SalirDelTurno() => Groups.RemoveFromGroupAsync(Context.ConnectionId, KardexTurnoGroup);

    public override async Task OnConnectedAsync()
    {
        // Sugerencia para los modulos: unirse a un grupo automatico por defecto, por ejemplo
        // $"operadores.ubicacion.{Context.UserIdentifier}", y emitir la ubicacion actual.
        await base.OnConnectedAsync();
    }
}
