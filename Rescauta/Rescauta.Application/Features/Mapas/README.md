# Modulo Mapas

Dueño de este módulo: **(asignar)**. Nadie mas escribe dentro de esta carpeta ni en
`Domain/Entities/Mapas`, `Features/Mapas` o `Controllers/v1/MapasController.cs`.

## Carpetas reservadas

```
Domain/Entities/Mapas/                          PuntoRescate, RutaEntrega, GeocodingSnapshot
Application/Features/Mapas/                     casos de uso (un slice por accion)
Infrastructure/Persistence/Configurations/Mapas/  mapeo EF Core de cada entidad
Infrastructure/Persistence/Repositories/Mapas/    repositorios, si hacen falta
Api/Controllers/v1/MapasController.cs            endpoints
```

## Alcance sugerido

- Geocodificacion y puntos de rescate.
- Rutas de reparto y optimizacion.
- Seguimiento de unidades en tiempo real (SignalR).

## Eventos de SignalR

Prefijo obligatorio: `Mapas_` para metodos cliente -> servidor, `mapas.` para eventos
servidor -> cliente.

```csharp
// En RescautaHub: no editar el archivo sin acuerdo; se agree la adicion de una linea.
public Task Mapas_SuscribirseRuta(string rutaId) => Groups.AddToGroupAsync(Context.ConnectionId, $"mapas.ruta.{rutaId}");
```

```csharp
// Emision desde un caso de uso:
await _notifier.SendToGroupAsync($"mapas.ruta.{rutaId}", "mapas.ruta.actualizada", payload, ct);
```

## Claves de cache

Prefijo obligatorio: `rescauta:mapas:`
