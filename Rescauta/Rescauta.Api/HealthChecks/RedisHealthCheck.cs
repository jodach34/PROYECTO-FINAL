using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rescauta.Application.Interfaces.Caching;

namespace Rescauta.Api.HealthChecks;

/// <summary>
/// Health check de la cache distribuida. Implementa <see cref="IHealthCheck"/> en vez de
/// usar un delegate para poder recibir <see cref="ICacheService"/> por constructor.
///
/// Devuelve Degraded (no Unhealthy) cuando Redis no responde: la API sigue sirviendo
/// peticiones, solo pierde cache. Un Unhealthy aqui tumbaria el servicio por un problema
/// que no es critico.
/// </summary>
public sealed class RedisHealthCheck(ICacheService cacheService) : IHealthCheck
{
    private const string ProbeKey = "rescauta:health:probe";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await cacheService.SetAsync(ProbeKey, "ok", TimeSpan.FromSeconds(15), cancellationToken);
            var readBack = await cacheService.GetAsync<string>(ProbeKey, cancellationToken);

            return readBack == "ok"
                ? HealthCheckResult.Healthy("Redis responde")
                : HealthCheckResult.Degraded("Redis no devolvio el valor de prueba");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis no disponible", ex);
        }
    }
}
