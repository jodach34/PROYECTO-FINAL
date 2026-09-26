namespace Rescauta.Infrastructure.Options;

/// <summary>
/// Se enlaza a la seccion "RabbitMq" de appsettings.json.
/// La cadena de conexion (URI amqps://) vive en "ConnectionStrings:RabbitMq", igual que
/// Redis vive en "ConnectionStrings:Redis". Aqui solo esta la politica, no el secreto.
///
/// La URI se pasa tal cual a <c>ConnectionFactory.Uri</c> (RabbitMQ.Client 7.x), que ya
/// deduce del esquema lo que hace falta:
///   amqp://  -> puerto 5672, sin TLS
///   amqps:// -> puerto 5671, con TLS y SNI
/// y toma usuario, contrasena y vhost de la propia URI.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// Interruptor de seguridad. En false NO se registra ningun servicio de RabbitMQ y la
    /// API arranca igual. Principio del proyecto: un broker caido no tumba la API.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Exchange de topic donde se publican los eventos del dominio.</summary>
    public string Exchange { get; set; } = "rescauta.events";

    /// <summary>Prefijo de las colas. Convencion: rescauta:{modulo}:{entidad}.</summary>
    public string QueuePrefix { get; set; } = "rescauta";

    /// <summary>Timeout para establecer la conexion TCP+TLS+AMQP.</summary>
    public int ConnectionTimeoutInSeconds { get; set; } = 10;

    /// <summary>Heartbeat AMQP. Detecta conexiones muertas tras una caida de red.</summary>
    public int HeartbeatInSeconds { get; set; } = 60;

    /// <summary>Reconexion automatica ante caida de red del broker.</summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>Rediscoveracion de colas y bindings al reconectar.</summary>
    public bool TopologyRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Mensajes sin confirmar por consumidor. 20 evita que un consumidor lento
    /// acapare la cola completa en memoria.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 20;
}
