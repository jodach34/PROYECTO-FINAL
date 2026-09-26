namespace Rescauta.Application.Interfaces.RealTime;

/// <summary>
/// Contrato de notificaciones en tiempo real. La implementacion que reenvia a SignalR
/// esta en Infrastructure/RealTime/RescautaRealtimeNotifier.cs.
///
/// Un caso de uso nunca injecta <c>IHubContext&lt;RescautaHub&gt;</c>: inyecta esto.
/// Asi Application no depende de ASP.NET Core.
/// </summary>
public interface IRescautaNotifier
{
    Task SendUpdateAsync(string eventName, object? payload, CancellationToken cancellationToken = default);

    Task SendToGroupAsync(string groupName, string eventName, object? payload, CancellationToken cancellationToken = default);
}
