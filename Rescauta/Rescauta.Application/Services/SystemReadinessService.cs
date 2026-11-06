using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.Caching;

namespace Rescauta.Application.Services;

/// <summary>
/// Plantilla de servicio de Application. Muestra el patron obligatorio para el equipo:
///   * depender solo de interfaces definidas en Application/Interfaces,
///   * recibir las dependencias por constructor,
///   * no tocar Infrastructure ni ASP.NET Core.
///
/// Sobre <c>IsReady</c>: lo decide SOLO la base de datos, no la cache. La cache es una
/// optimizacion, no un requisito: sin ella el sistema lee y escribe igual, solo mas lento.
/// Si IsReady exigiera tambien la cache, un Redis caido devolveria 503 y el orquestador
/// reiniciaria pods que funcionan perfectamente, tirando abajo el servicio por un problema
/// que no afecta la disponibilidad. El estado de la cache sigue viajando en
/// <c>CacheAvailable</c> y en <c>Details</c> para diagnóstico, que es donde sirve.
/// </summary>
public sealed class SystemReadinessService(
    IAppDbContext dbContext,
    ICacheService cacheService,
    ILogger<SystemReadinessService> logger) : ISystemReadinessService
{
    private const string ReadinessCacheKey = "rescauta:system:readiness:v1";

    private static readonly TimeSpan CacheProbeTimeout = TimeSpan.FromSeconds(2);

    public async Task<ReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        var details = new List<string>();

        var databaseAvailable = await ProbeDatabaseAsync(details, cancellationToken);
        var cacheAvailable = await ProbeCacheAsync(details, cancellationToken);

        logger.LogInformation(
            "Readiness evaluada. Database={Database}, Cache={Cache}",
            databaseAvailable,
            cacheAvailable);

        return new ReadinessReport(
            databaseAvailable,
            databaseAvailable,
            cacheAvailable,
            dbContext.Database.ProviderName ?? "desconocido",
            details);
    }

    private async Task<bool> ProbeDatabaseAsync(List<string> details, CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await dbContext.PingAsync(cancellationToken);
            details.Add(canConnect ? "Base de datos: OK" : "Base de datos: sin conexion");

            return canConnect;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo la comprobacion de la base de datos");
            details.Add($"Base de datos: error ({ex.GetType().Name})");

            return false;
        }
    }

    private async Task<bool> ProbeCacheAsync(List<string> details, CancellationToken cancellationToken)
    {
        // Tope duro de tiempo: si Redis no responde, el endpoint /readiness debe
        // responder rapido con cacheAvailable=false, no colgarse varios segundos.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CacheProbeTimeout);

        try
        {
            var probeKey = $"{ReadinessCacheKey}:probe";
            await cacheService.SetAsync(probeKey, "ok", TimeSpan.FromSeconds(30), timeout.Token);
            var readBack = await cacheService.GetAsync<string>(probeKey, timeout.Token);
            await cacheService.RemoveAsync(probeKey, timeout.Token);

            var isAvailable = readBack == "ok";
            details.Add(isAvailable ? "Cache: OK" : "Cache: lectura vacia");

            return isAvailable;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("La cache no respondio dentro de {Timeout} ms", CacheProbeTimeout.TotalMilliseconds);
            details.Add("Cache: timeout");

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo la comprobacion de la cache distribuida");
            details.Add($"Cache: no disponible ({ex.GetType().Name})");

            return false;
        }
    }
}
