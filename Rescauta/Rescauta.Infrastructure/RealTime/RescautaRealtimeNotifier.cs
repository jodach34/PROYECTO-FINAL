using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Infrastructure.Hubs;

namespace Rescauta.Infrastructure.RealTime;

/// <summary>
/// Traduce las notificaciones de Application a llamadas de SignalR.
/// Es el unico punto del sistema que conoce a la vez Application y el Hub,
/// por eso vive en Infrastructure y no en Application.
/// </summary>
public sealed class RescautaRealtimeNotifier(
    IHubContext<RescautaHub> hubContext,
    ILogger<RescautaRealtimeNotifier> logger) : IRescautaNotifier
{
    public async Task SendUpdateAsync(string eventName, object? payload, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Difusion de evento {EventName} a todos los clientes", eventName);

        await hubContext.Clients.All.SendAsync(eventName, payload, cancellationToken);
    }

    public async Task SendToGroupAsync(
        string groupName,
        string eventName,
        object? payload,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Difusion de evento {EventName} al grupo {GroupName}", eventName, groupName);

        await hubContext.Clients.Group(groupName).SendAsync(eventName, payload, cancellationToken);
    }
}
