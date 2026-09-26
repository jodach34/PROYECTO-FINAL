using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Rescauta.Infrastructure.Options;

namespace Rescauta.Infrastructure.Messaging;

/// <summary>
/// Punto unico de acceso a la conexion AMQP.
///
/// Se registra como singleton y abre la conexion de forma perezosa: el composition root
/// no toca la red al arrancar, y si el broker no responde la API levanta igual.
///
/// La URI de "ConnectionStrings:RabbitMq" se pasa a <see cref="ConnectionFactory.Uri"/>, que
/// deduce del esquema lo que hace falta (amqps -> puerto 5671 + TLS y SNI) y toma usuario,
/// contrasena y vhost de la propia URI.
///
/// Politica de degradacion, misma que Redis: si el broker no esta, se registra el error y
/// el llamador recibe false. Publicar en una cola no puede ser motivo para tumbar el sistema.
/// </summary>
public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    IConfiguration configuration,
    ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private IConnection? connection;
    private bool exchangeDeclared;

    /// <summary>
    /// Devuelve la conexion abierta, creandola si hace falta, o null si el broker no esta
    /// disponible. Nunca lanza: el broker caido se degrada, no propaga.
    /// </summary>
    public async Task<IConnection?> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await gate.WaitAsync(cancellationToken);

        try
        {
            // Reconexion: si quedo una conexion cerrada, se descarta antes de rehacerla.
            if (connection is not null)
            {
                await connection.DisposeAsync();
                connection = null;
                exchangeDeclared = false;
            }

            var settings = options.Value;
            var uri = configuration.GetConnectionString("RabbitMq");

            if (string.IsNullOrWhiteSpace(uri))
            {
                logger.LogWarning(
                    "No se encontro ConnectionStrings:RabbitMq. La API arranca SIN mensajeria. " +
                    "Definir la URI amqps:// en secretos de usuario o variables de entorno.");

                return null;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri),
                AutomaticRecoveryEnabled = settings.AutomaticRecoveryEnabled,
                TopologyRecoveryEnabled = settings.TopologyRecoveryEnabled,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(settings.ConnectionTimeoutInSeconds),
                RequestedHeartbeat = TimeSpan.FromSeconds(settings.HeartbeatInSeconds),
                ClientProvidedName = "rescauta-api"
            };

            connection = await factory.CreateConnectionAsync(cancellationToken);

            logger.LogInformation(
                "Conexion AMQP establecida en {Endpoint} (vhost {VirtualHost})",
                connection.Endpoint,
                factory.VirtualHost);

            await EnsureExchangeAsync(connection, settings, cancellationToken);

            return connection;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "No se pudo abrir la conexion AMQP. La API sigue operando sin mensajeria (degradado).");

            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Publica un cuerpo JSON en el exchange de la seccion "RabbitMq". Devuelve false si no
    /// se pudo publicar, para que el llamador decida si reintenta o sigue adelante.
    /// </summary>
    public async Task<bool> PublishJsonAsync(
        string routingKey,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var current = await GetConnectionAsync(cancellationToken);

        if (current is null)
        {
            return false;
        }

        try
        {
            var settings = options.Value;
            await using var channel = await current.CreateChannelAsync(cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(payload);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                exchange: settings.Exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Fallo la publicacion en AMQP con routing key {RoutingKey}", routingKey);

            return false;
        }
    }

    /// <summary>
    /// Declara el exchange de eventos una vez por conexion. Sin esto, publicar a un exchange
    /// inexistente cierra el canal con 404 NOT_FOUND y el primer PublishAsync falla.
    /// "topic" es lo que permite que un consumidor se suscriba a "rescauta.donaciones.#".
    /// </summary>
    private async Task EnsureExchangeAsync(
        IConnection current,
        RabbitMqOptions settings,
        CancellationToken cancellationToken)
    {
        if (exchangeDeclared)
        {
            return;
        }

        await using var channel = await current.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: settings.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken);

        exchangeDeclared = true;

        logger.LogInformation("Exchange {Exchange} declarado (topic, durable)", settings.Exchange);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }

        gate.Dispose();
    }
}
