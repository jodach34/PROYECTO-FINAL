using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Donaciones;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Api.HealthChecks;

/// <summary>
/// Health check de la base de datos.
///
/// No basta con SELECT 1: SQLite responde aunque el archivo exista y no tenga ni una
/// tabla, y en ese caso Render daria el despliegue por bueno mientras todas las
/// consultas reales fallan. Por eso, despues del ping, se consultan las tablas
/// esenciales del sistema. Cualquier tabla faltante se reporta como Unhealthy.
/// </summary>
public sealed class DatabaseHealthCheck(IAppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isAvailable = await dbContext.PingAsync(cancellationToken);

        if (!isAvailable)
        {
            return HealthCheckResult.Unhealthy("La base de datos no responde a SELECT 1");
        }

        try
        {
            // AnyAsync valida que la tabla exista y sea consultable. Si esta vacia
            // devuelve false, pero sin lanzar: la base sigue siendo valida.
            await dbContext.Set<Comedor>().AnyAsync(cancellationToken);
            await dbContext.Set<Insumo>().AnyAsync(cancellationToken);
            await dbContext.Set<Donacion>().AnyAsync(cancellationToken);
            await dbContext.Set<Usuario>().AnyAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                $"Base de datos OK ({dbContext.Database.ProviderName ?? "desconocido"}), tablas esenciales disponibles");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "La base de datos responde, pero falta el esquema esencial o no se puede consultar.",
                ex);
        }
    }
}
