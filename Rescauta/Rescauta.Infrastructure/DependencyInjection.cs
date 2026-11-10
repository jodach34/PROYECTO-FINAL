using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Features.Autenticacion;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.Ai;
using Rescauta.Application.Interfaces.Caching;
using Rescauta.Application.Interfaces.Messaging;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Infrastructure.Ai;
using Rescauta.Infrastructure.Caching;
using Rescauta.Infrastructure.Email;
using Rescauta.Infrastructure.Messaging;
using Rescauta.Infrastructure.Options;
using Rescauta.Infrastructure.Persistence;
using Rescauta.Infrastructure.Persistence.Interceptors;
using Rescauta.Infrastructure.Products;
using Rescauta.Infrastructure.RealTime;
using Rescauta.Infrastructure.Security;
using Rescauta.Infrastructure.Services;

namespace Rescauta.Infrastructure;

/// <summary>
/// Registro de TODA la infraestructura. La API lo invoca una sola vez desde Program.cs
/// con <c>builder.Services.AddInfrastructure(builder.Configuration)</c>.
///
/// Objetivo para el equipo: mientras este archivo no necesite cambios, los 3 modulos
/// pueden trabajar en paralelo sin editar el composition root.
/// </summary>
public static class DependencyInjection
{
    public const string DatabaseProviderSqlite = "Sqlite";
    public const string DatabaseProviderSqlServer = "SqlServer";
    public const string DatabaseProviderPostgres = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"Falta configurar '{DatabaseOptions.SectionName}:ConnectionString'.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName));

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));

        services.AddOptions<QrOptions>()
            .Bind(configuration.GetSection(QrOptions.SectionName));

        AddAiClients(services, configuration);
        AddOpenFoodFactsClient(services, configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddSingleton<CacheKeyRegistry>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IRescautaNotifier, RescautaRealtimeNotifier>();
        services.TryAddSingleton<IPasswordHashService, PasswordHashService>();
        services.TryAddScoped<IEmailSender, SmtpEmailSender>();

        // Generador del QR publico de donacion. Scoped aunque sea stateless, para no tener
        // que recordar que es seguro como singleton si manana dependiera de un servicio
        // con estado por peticion.
        services.TryAddScoped<IQrGeneratorService, QrGeneratorService>();

        // Modulo Kardex: notificador tipado de inventario sobre el hub compartido.
        // Singleton, no Scoped ni Transient: IHubContext<RescautaHub> es singleton con
        // vida corta por conexion y seguro de compartir, asi que el notificador no tiene
        // estado por peticion. Scoped crearia una instancia inútil en cada request.
        services.AddSingleton<IInventoryNotifier, SignalRInventoryNotifier>();
        services.TryAddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        AddRabbitMq(services, configuration);

        services.AddDbContext<AppDbContext>((provider, options) =>
            ConfigureDatabase(options, configuration, provider));

        return services;
    }

    /// <summary>
    /// Registra los clientes HTTP tipados del chatbot (Gemini y Qdrant).
    ///
    /// No se usa ValidateOnStart a proposito: la API debe arrancar sin claves de IA para
    /// que el resto del sistema y el chatbot actual sigan funcionando. Si falta una clave,
    /// el cliente lo reporta en su comprobacion de conectividad, sin tumbar el proceso.
    ///
    /// La ApiKey nunca se guarda en appsettings.json: se enlaza desde user-secrets o desde
    /// las variables de entorno Gemini__ApiKey / Qdrant__ApiKey (ver README y render.yaml).
    /// </summary>
    private static void AddAiClients(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName));

        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName));

        services.AddOptions<AiIndexingOptions>()
            .Bind(configuration.GetSection(AiIndexingOptions.SectionName));

        services.AddOptions<AiRagOptions>()
            .Bind(configuration.GetSection(AiRagOptions.SectionName));

        // La comprobación de secretos necesita conocer las claves, pero Application
        // solo consume la abstracción ISecretRedactor.
        services.TryAddSingleton<ISecretRedactor, ConfiguredSecretRedactor>();

        // Los documentos institucionales son estáticos y de solo lectura: un singleton
        // evita releer los recursos embebidos en cada petición de indexación.
        services.TryAddSingleton<IDocumentSourceProvider, MarkdownDocumentSourceProvider>();

        services.AddHttpClient<IGeminiClient, GeminiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddHttpClient<IQdrantClient, QdrantClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<QdrantOptions>>().Value;

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
    }

    /// <summary>
    /// Cliente tipado de Open Food Facts para el autocompletado del alta de insumos.
    /// Sin ValidateOnStart: la API debe arrancar aunque la fuente externa no responda.
    /// </summary>
    private static void AddOpenFoodFactsClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenFoodFactsOptions>()
            .Bind(configuration.GetSection(OpenFoodFactsOptions.SectionName));

        services.AddHttpClient<IOpenFoodFactsClient, OpenFoodFactsClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenFoodFactsOptions>>().Value;

            var baseUrl = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                ? uri
                : new Uri("https://world.openfoodfacts.org/");

            client.BaseAddress = baseUrl;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Rescauta/1.0");
        });
    }

    /// <summary>
    /// Registra la mensajeria (RabbitMQ / CloudAMQP) si "RabbitMq:Enabled" lo permite.
    /// Con Enabled en false se registra NullEventBus: IEventBus siempre resuelve, de modo que
    /// los modulos pueden publicar sin saber si el broker esta disponible, y la API arranca
    /// sin broker levantado.
    /// </summary>
    private static void AddRabbitMq(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName));

        var settings = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                       ?? new RabbitMqOptions();

        // Habilitada exige las DOS cosas: Enabled en true y una URI real. Con Enabled en true
        // y la cadena vacia no hay broker al que conectarse, y RabbitMqEventBus avisaria por
        // log en cada publicacion. Se registra NullEventBus para que el modulo publish sin
        // enterarse de que no hay mensajeria, que es el mismo contrato en los dos casos.
        if (!settings.Enabled ||
            string.IsNullOrWhiteSpace(configuration.GetConnectionString("RabbitMq")))
        {
            services.TryAddScoped<IEventBus, NullEventBus>();

            return;
        }

        // Singleton + IAsyncDisposable: el contenedor lo cierra solo al apagar la app.
        // El bus es scoped porque envelopa el evento con datos de la peticion en curso.
        services.AddSingleton<RabbitMqConnection>();
        services.TryAddScoped<IEventBus, RabbitMqEventBus>();
    }

    private static void ConfigureDatabase(
        DbContextOptionsBuilder options,
        IConfiguration configuration,
        IServiceProvider provider)
    {
        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                      ?? new DatabaseOptions();

        if (string.IsNullOrWhiteSpace(database.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexion. Configurar '{DatabaseOptions.SectionName}:ConnectionString' " +
                "en appsettings.json, appsettings.{Environment}.json o en secretos de usuario.");
        }

        var migrationsAssembly = typeof(AppDbContext).Assembly.FullName;
        var providerName = database.Provider?.Trim().ToLowerInvariant() ?? string.Empty;

        if (providerName == DatabaseProviderSqlite.ToLowerInvariant())
        {
            options.UseSqlite(
                database.ConnectionString,
                sqlite => sqlite.MigrationsAssembly(migrationsAssembly));
        }
        else if (providerName == DatabaseProviderSqlServer.ToLowerInvariant())
        {
            options.UseSqlServer(
                database.ConnectionString,
                sqlServer => sqlServer.MigrationsAssembly(migrationsAssembly));
        }
        else if (providerName == DatabaseProviderPostgres.ToLowerInvariant())
        {
            options.UseNpgsql(
                database.ConnectionString,
                npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
        }
        else
        {
            throw new InvalidOperationException(
                $"Proveedor de base de datos desconocido: '{database.Provider}'. " +
                $"Valores admitidos: {DatabaseProviderSqlite}, {DatabaseProviderSqlServer}, {DatabaseProviderPostgres}.");
        }

        if (database.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }

        options.AddInterceptors(provider.GetRequiredService<AuditableEntityInterceptor>());
    }
}
