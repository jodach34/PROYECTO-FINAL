using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces.Messaging;
using Rescauta.Domain.Common;

namespace Rescauta.Infrastructure.Messaging;

/// <summary>
/// Implementacion de <see cref="IEventBus"/> que no publica en ningun broker.
///
/// Es la que se registra cuando "RabbitMq:Enabled" es false, para que los modulos puedan
/// llamar a IEventBus sin comprobar si la mensajeria esta activa: siempre resuelve, siempre
/// devuelve false. Evita el fallo de inyeccion en tiempo de ejecucion y permite a un dev
/// trabajar sin broker levantado.
/// </summary>
public sealed class NullEventBus(ILogger<NullEventBus> logger) : IEventBus
{
    public Task<bool> PublishAsync(DomainEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Mensajeria deshabilitada: el evento {EventType} no se publica",
            @event.GetType().Name);

        return Task.FromResult(false);
    }

    public Task<bool> PublishAsync(
        DomainEvent @event,
        string routingKey,
        CancellationToken cancellationToken = default) =>
        PublishAsync(@event, cancellationToken);
}
