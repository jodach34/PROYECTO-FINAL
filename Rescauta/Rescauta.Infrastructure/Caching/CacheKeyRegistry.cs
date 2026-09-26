using System.Collections.Concurrent;

namespace Rescauta.Infrastructure.Caching;

/// <summary>
/// Indice en memoria de las claves de cache creadas por la aplicacion.
/// Existe porque <c>IDistributedCache</c> no permite buscar por patron: para que cada
/// modulo pueda invalidar su propio espacio de nombres (por ejemplo
/// "rescauta:mapas:") se registra aqui toda clave al escribirla.
///
/// Se registra como singleton. Es deliberadamente simple y suficiente para el volumen
/// esperado; si algun dia hace falta, se puede migrar a un indice persistido en Redis.
/// </summary>
public sealed class CacheKeyRegistry
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    public void Track(string key) => _keys.TryAdd(key, 0);

    public void Untrack(string key) => _keys.TryRemove(key, out _);

    public IReadOnlyCollection<string> GetKeys(string keyPrefix) => _keys.Keys
        .Where(key => key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
        .ToArray();
}
