namespace Rescauta.Application.Interfaces;

/// <summary>
/// Ejemplo de servicio de Application: verifica que la infraestructura responde.
/// Sirve como plantilla de DI para los servicios de Mapas, Kardex y Donaciones,
/// y alimenta el endpoint /api/v1/system/readiness.
/// </summary>
public interface ISystemReadinessService
{
    Task<ReadinessReport> GetReadinessAsync(CancellationToken cancellationToken = default);
}

public sealed record ReadinessReport(
    bool IsReady,
    bool DatabaseAvailable,
    bool CacheAvailable,
    string DatabaseProvider,
    IReadOnlyList<string> Details);
