using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.Caching;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Domain.Repositories;
using Rescauta.Infrastructure.Caching;
using Rescauta.Infrastructure.Options;
using Rescauta.Infrastructure.Persistence;
using Rescauta.Infrastructure.Persistence.Interceptors;
using Rescauta.Infrastructure.Persistence.Repositories;
using Rescauta.Infrastructure.RealTime;
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

        services.AddHttpContextAccessor();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddSingleton<CacheKeyRegistry>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IRescautaNotifier, RescautaRealtimeNotifier>();
        services.TryAddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Adaptadores de las abstracciones de Domain. Todos Scoped porque dependen del
        // DbContext scoped: un singleton aqui compartiria estado entre peticiones.
        services.TryAddScoped<IComedorRepository, ComedorRepository>();
        services.TryAddScoped<IKardexRepository, KardexRepository>();
        services.TryAddScoped<IDonacionRepository, DonacionRepository>();

        // Generador de QR de donacion. Scoped aunque sea stateless, para no tener que
        // recordar que es seguro como singleton si mañana cachea un HttpClient.
        services.TryAddScoped<IQrGeneratorService, QrGeneratorService>();

        services.AddDbContext<AppDbContext>((provider, options) =>
            ConfigureDatabase(options, configuration, provider));

        return services;
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
