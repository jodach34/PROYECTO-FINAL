using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Autenticacion;
using Rescauta.Infrastructure.Options;
using Rescauta.Infrastructure.Persistence;
using Rescauta.Infrastructure.Persistence.Seed;

namespace Rescauta.Infrastructure.Extensions;

/// <summary>
/// Aplica las migraciones de EF Core al arrancar en desarrollo.
///
/// Decisión de equipo: las migraciones se generan y aplican desde la rama principal,
/// nunca desde una rama de módulo. Motivo: si 3 devs generan migraciones en paralelo,
/// EF Core produce una sucesión de migraciones que se rompen al integrarlas.
/// Flujo acordado:
///   1) Cada dev genera su migración en su rama con prefijo de módulo:
///        dotnet ef migrations add Kardex_InitialCreate -p Rescauta.Infrastructure
///   2) Abre PR contra main. La persona "dueña del esquema" la revisa.
///   3) main aplica las migraciones pendientes al desplegar.
///
/// Si no hay migraciones generadas (caso que ya no ocurre en este repositorio), el
/// método cae en EnsureCreated para que el SQLite de desarrollo quede utilizable de
/// inmediato. En produccion, con failFast en true, un error de conexion o de esquema
/// detiene el arranque en vez de dejar la API respondiendo sin tablas.
/// </summary>
public static class PersistenceExtensions
{
    public static async Task ApplyPendingMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default,
        bool failFast = false,
        bool seedDemoData = false,
        bool requireAdmin = false)
    {
        using var scope = services.CreateScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(PersistenceExtensions).FullName!);

        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!options.MigrateOnStartup)
        {
            logger.LogInformation("Database:MigrateOnStartup = false. Se omite la migracion automatica.");

            return;
        }

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Ping real y no CanConnectAsync: con SQLite, CanConnect devuelve false cuando
            // el archivo no existe todavia, y en un clon nuevo recien bajado eso
            // abortaria la creacion de la base de datos.
            if (!await context.PingAsync())
            {
                if (failFast)
                {
                    throw new InvalidOperationException(
                        "La base de datos no responde. En produccion la API no arranca sin esquema.");
                }

                logger.LogWarning("La base de datos no responde. Se omite la migracion.");

                return;
            }

            var migrations = context.Database.GetMigrations().ToArray();

            if (migrations.Length == 0)
            {
                logger.LogInformation(
                    "Todavia no hay migraciones generadas. Se crea el esquema con EnsureCreated para el proveedor {Provider}.",
                    options.Provider);

                await context.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                var pending = context.Database.GetPendingMigrations().ToArray();

                if (pending.Length == 0)
                {
                    logger.LogInformation("La base de datos esta al dia. Migraciones pendientes: 0.");
                }
                else
                {
                    logger.LogInformation("Aplicando {Count} migracion(es) pendiente(s).", pending.Length);

                    await context.Database.MigrateAsync(cancellationToken);
                }
            }

            await AuthSchemaInitializer.AsegurarAsync(context, logger, cancellationToken);

            // La semilla va DESPUES de crear o migrar el esquema, nunca antes: si se intentara
            // antes, el INSERT fallaria por tabla inexistente en una base recien bajada.
            //
            // Y va tambien cuando no havia nada que migrar. Por eso el "return" del caso
            // "base al dia" se quito: con el, una base recien creada con la primera
            // migracion aplicada se quedaria sin datos para siempre, porque en el segundo
            // arranque no volveria a pasar por aqui.
            if (seedDemoData)
            {
                await DatosSemilla.SembrarAsync(context, logger, cancellationToken);
            }
            else
            {
                logger.LogInformation(
                    "Datos demo omitidos: solo se siembran en Development.");
            }

            var authOptions = scope.ServiceProvider
                .GetRequiredService<IOptions<AuthOptions>>()
                .Value;

            var passwordHashService = scope.ServiceProvider
                .GetRequiredService<IPasswordHashService>();

            await UsuarioSemilla.SembrarAsync(
                context,
                passwordHashService,
                authOptions,
                logger,
                requireAdmin,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No se pudieron aplicar las migraciones. Revisar Database:Provider y Database:ConnectionString.");

            // En desarrollo se registra y se sigue para que el dev pueda corregir la
            // configuracion. En produccion se relanza: Render debe ver el despliegue
            // fallido, no una API viva que responde 500 en cada consulta por falta de tablas.
            if (failFast)
            {
                throw;
            }
        }
    }
}
