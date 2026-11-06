using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces.Messaging;
using Rescauta.Domain.Common;

namespace Rescauta.Infrastructure.Messaging;

/// <summary>
/// Implementacion de <see cref="IEventBus"/> sobre RabbitMQ / CloudAMQP.
///
/// Deriva el routing key del tipo del evento para que ningun modulo tenga que hardcodear
//  strings. Se aplica la convencion de namespaces del proyecto:
///   Rescauta.Domain.Events.Donaciones.DonacionRegistrada
///     -> rescauta.donaciones.donacionregistrada
///   Rescauta.Domain.Events.Mapas.PuntoRescateActualizado
///     -> rescauta.mapas.puntorescateactualizado
/// El exchange es de tipo topic, asi que un consumidor nuevo se suscribe a
/// "rescauta.donaciones.#" sin tocar al productor.
///
/// Si el namespace no sigue la convencion, el routing key degrada a "rescauta.{evento}":
/// sigue siendo un topic valido, solo pierde el segmento de modulo.
///
/// El cuerpo va envuelto en un sobre con EventId, Type, OccurredOn y Payload para que un
/// consumidor de otro lenguaje sepa interpretarlo sin conocer el CLR.
/// </summary>
public sealed class RabbitMqEventBus(
    RabbitMqConnection connection,
    ILogger<RabbitMqEventBus> logger) : IEventBus
{
    private const string RoutingKeyPrefix = "rescauta";

    private static readonly string[] KnownModules = ["mapas", "kardex", "donaciones"];

    public Task<bool> PublishAsync(DomainEvent @event, CancellationToken cancellationToken = default)
    {
        var routingKey = BuildRoutingKey(@event);

        logger.LogDebug("Publicando {EventType} en {RoutingKey}", @event.GetType().Name, routingKey);

        return connection.PublishJsonAsync(routingKey, BuildEnvelope(@event), cancellationToken);
    }

    public Task<bool> PublishAsync(
        DomainEvent @event,
        string routingKey,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Publicando {EventType} en {RoutingKey} (routing key explicito)", @event.GetType().Name, routingKey);

        return connection.PublishJsonAsync(routingKey, BuildEnvelope(@event), cancellationToken);
    }

    private static string BuildRoutingKey(DomainEvent @event)
    {
        var type = @event.GetType();
        var segments = new List<string> { RoutingKeyPrefix };
        var module = ResolveModule(type.Namespace);

        if (module is not null)
        {
            segments.Add(module);
        }

        segments.Add(type.Name.ToLowerInvariant());

        return string.Join('.', segments);
    }

    /// <summary>
    /// Busca el modulo como el segmento inmediatamente posterior a "Events" en el namespace
    /// (Rescauta.Domain.Events.{Modulo}), y solo lo acepta si es un modulo conocido del
    /// proyecto. Asi un namespace con otra forma no inventa un modulo inexistente.
    /// </summary>
    private static string? ResolveModule(string? typeNamespace)
    {
        if (string.IsNullOrWhiteSpace(typeNamespace))
        {
            return null;
        }

        var parts = typeNamespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(parts, part => part.Equals("Events", StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= parts.Length)
        {
            return null;
        }

        var candidate = parts[index + 1].ToLowerInvariant();

        return KnownModules.Contains(candidate) ? candidate : null;
    }

    private static EventEnvelope<object> BuildEnvelope(DomainEvent @event) => new(
        @event.EventId,
        @event.GetType().Name,
        @event.OccurredOn,
        @event);

    private sealed record EventEnvelope<TPayload>(
        Guid EventId,
        string Type,
        DateTimeOffset OccurredOn,
        TPayload Payload);
}
