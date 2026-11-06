using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rescauta.Application.Interfaces;

namespace Rescauta.Api.HealthChecks;

/// <summary>
/// Health check de la base de datos.
///
/// Sustituye a <c>AddDbContextCheck&lt;T&gt;()</c> a proposito: aquel usa
/// <c>Database.CanConnectAsync()</c>, que con SQLite devuelve false cuando el archivo
/// todavia no existe. En un clon nuevo eso marcaria la base como caida siendo que
/// todavia no hay nada que crear. <see cref="IAppDbContext.PingAsync"/> ejecuta un
/// "SELECT 1" real y no miente en ningun proveedor.
/// </summary>
public sealed class DatabaseHealthCheck(IAppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isAvailable = await dbContext.PingAsync(cancellationToken);

        return isAvailable
            ? HealthCheckResult.Healthy(
                $"Base de datos OK ({dbContext.Database.ProviderName ?? "desconocido"})")
            : HealthCheckResult.Unhealthy("La base de datos no responde a SELECT 1");
    }
}
