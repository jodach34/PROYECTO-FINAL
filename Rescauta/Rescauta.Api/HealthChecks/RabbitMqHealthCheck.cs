using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using Rescauta.Infrastructure.Messaging;

namespace Rescauta.Api.HealthChecks;

/// <summary>
/// Health check de la mensajeria. Implementa <see cref="IHealthCheck"/> para poder recibir
/// <see cref="RabbitMqConnection"/> por constructor.
///
/// Devuelve Degraded (nunca Unhealthy) cuando el broker no responde, igual que Redis: la
/// API sigue sirviendo peticiones y solo pierde la publicacion de eventos. Un Unhealthy aqui
/// tumbaria el servicio por una dependencia no critica.
///
/// A diferencia del check de Redis, este NO escribe en el broker: solo comprueba que haya
/// conexion. Sondear publicando mensajes de prueba ensuciaria la cola en produccion.
/// </summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var current = await connection.GetConnectionAsync(cancellationToken);

            if (current is null)
            {
                return HealthCheckResult.Degraded("Broker AMQP no disponible");
            }

            return current.IsOpen
                ? HealthCheckResult.Healthy($"Broker AMQP conectado ({current.Endpoint})")
                : HealthCheckResult.Degraded("Conexion AMQP cerrada");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Broker AMQP no disponible", ex);
        }
    }
}
