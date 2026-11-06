using Rescauta.Domain.Common;

namespace Rescauta.Application.Interfaces.Messaging;

/// <summary>
/// Contrato de publicacion de eventos. La implementacion real usa RabbitMQ / CloudAMQP y
/// esta en Infrastructure/Messaging/RabbitMqEventBus.cs.
///
/// Por que existe: los modulos (Mapas, Kardex, Donaciones) viven en Application y no pueden
/// referenciar Infrastructure. Sin esta interfaz no tendrian forma de publicar eventos sin
/// violar las reglas de dependencia del proyecto.
///
/// Reglas para los modulos:
///   * Publicar SIEMPRE un <see cref="DomainEvent"/>, nunca una entidad de EF.
///     Los eventos cruzan capas: solo ids, Guid, decimales, fechas y cadenas.
///   * No inventar el routing key: se deriva solo del nombre del tipo
///     (DonacionRegistrada -> rescauta.donaciones.donacionregistrada).
///   * La publicacion NUNCA es critica para la operacion de negocio. Si el broker esta caido
///     se registra el error y la peticion sigue; el modulo puede compensar con un
///     outbox pattern mas adelante.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publica un evento de dominio. Devuelve true si el broker lo recibio, false si no
    /// estaba disponible. No lanza excepcion por indisponibilidad del broker.
    /// </summary>
    Task<bool> PublishAsync(DomainEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publica un evento con routing key explicito. Reservado para integraciones externas
    /// (correos, webhooks) donde el destino no se deduce del tipo.
    /// </summary>
    Task<bool> PublishAsync(DomainEvent @event, string routingKey, CancellationToken cancellationToken = default);
}
