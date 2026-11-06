# Modulo Kardex / Inventario

Dueño de este módulo: **(asignar)**. Nadie mas escribe dentro de esta carpeta ni en
`Domain/Entities/Kardex`, `Features/Kardex` o `Controllers/v1/KardexController.cs`.

## Carpetas reservadas

```
Domain/Entities/Kardex/                            Articulo, MovimientoInventario, Almacen
Application/Features/Kardex/                       casos de uso
Infrastructure/Persistence/Configurations/Kardex/  mapeo EF Core
Infrastructure/Persistence/Repositories/Kardex/    repositorios
Api/Controllers/v1/KardexController.cs             endpoints
```

## Alcance sugerido

- Articulos, existencias y alchemyques.
- Kardex de movimientos (entradas, salidas, ajustes) con trazabilidad inmutable.
- Resumen de inventario por almacen y por articulo.

## Reglas del modulo

1. El kardex es **append-only**: un movimiento no se edita ni se borra. Una correccion
   se registra como un movimiento nuevo que lo reversa. Esto evita que tres devs peleen
   por el mismo registro.
2. El saldo de existencias se recalcula desde los movimientos, no se edita a mano.
3. Todo movimiento debe quedar registrado aunque la aplicacion se caiga: usar
   transaccion explicita y, si aplica, un outbox.

## Eventos de SignalR

Prefijo obligatorio: `Kardex_` (cliente -> servidor) y `kardex.` (servidor -> cliente).

```csharp
await _notifier.SendUpdateAsync("kardex.movimiento.registrado", payload, ct);
```

## Claves de cache

Prefijo obligatorio: `rescauta:kardex:`
Al cambiar existencias, invalidar con `RemoveByPrefixAsync("rescauta:kardex:")`.
