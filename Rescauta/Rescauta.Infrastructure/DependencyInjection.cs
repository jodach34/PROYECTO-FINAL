using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.Caching;
using Rescauta.Application.Interfaces.Messaging;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Infrastructure.Caching;
using Rescauta.Infrastructure.Messaging;
using Rescauta.Infrastructure.Options;
using Rescauta.Infrastructure.Persistence;
using Rescauta.Infrastructure.Persistence.Interceptors;
using Rescauta.Infrastructure.RealTime;

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

        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddSingleton<CacheKeyRegistry>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IRescautaNotifier, RescautaRealtimeNotifier>();

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

        if (!settings.Enabled)
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
