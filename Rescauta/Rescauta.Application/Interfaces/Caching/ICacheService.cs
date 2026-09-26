namespace Rescauta.Application.Interfaces.Caching;

/// <summary>
/// Contrato de cache distribuida. La implementacion real usa Redis y esta en
/// Infrastructure/Caching/RedisCacheService.cs.
///
/// Regla para los modulos: invalidar por prefijo de modulo, nunca por clave global.
/// Ejemplos de clave: "rescauta:mapas:puntos:v1", "rescauta:kardex:articulo:{id}:v1",
/// "rescauta:donaciones:resumen:v1".
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el valor cacheado o lo produce, lo guarda y lo devuelve. Es la forma
    /// preferida de leer para los listados de los tres modulos.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);
}
