using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces.Caching;

namespace Rescauta.Infrastructure.Caching;

/// <summary>
/// Implementacion de <see cref="ICacheService"/> sobre la cache distribuida de Redis
/// (<c>IDistributedCache</c> registrada en Program.cs con AddStackExchangeRedisCache).
///
/// Politica de degradacion: si Redis no responde, el cascarón NO revienta. Registra el
/// error, devuelve null en las lecturas y sigue operando contra la base de datos. En un
/// sistema de rescate comunitario perder la cache es molesto; caerse por la cache no es
/// una opcion.
///
/// Convencion de claves, obligatoria para los 3 modulos:
///   rescauta:{modulo}:{entidad}:{id|slug}:{version}
///   rescauta:mapas:puntos:{guid}:v1
///   rescauta:kardex:articulos:v1
///   rescauta:donaciones:resumen:v1
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache cache,
    CacheKeyRegistry keyRegistry,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await cache.GetStringAsync(key, cancellationToken);

            return payload is null ? default : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo la lectura de cache para la clave {CacheKey}", key);

            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);

            await cache.SetStringAsync(
                key,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration,
                    SlidingExpiration = TimeSpan.FromMinutes(1)
                },
                cancellationToken);

            keyRegistry.Track(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo la escritura de cache para la clave {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo la invalidacion de la clave {CacheKey}", key);
        }
        finally
        {
            keyRegistry.Untrack(key);
        }
    }

    public async Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in keyRegistry.GetKeys(keyPrefix))
        {
            await RemoveAsync(key, cancellationToken);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var created = await factory(cancellationToken);

        await SetAsync(key, created, expiration, cancellationToken);

        return created;
    }
}
