# Domain - Entidades

Este archivo solo contiene la guia de convenciones. Las entidades de negocio van en las
carpetas por modulo, y **cada dev escribe exclusivamente dentro de su propio modulo**:

```
Entities/
  Mapas/            -> PuntoRescate, RutaEntrega, GeocodingSnapshot...
  Kardex/           -> Articulo, MovimientoInventario, Almacen...
  Donaciones/       -> Donante, Donacion, EstadoDonacion...
  Compartido/       -> solo entidades usadas por mas de un modulo
```

## Reglas invariables (revisar en code review)

1. Una entidad = una clase. Hereda de `Rescauta.Domain.Common.BaseEntity`.
2. Si es raiz de agregado, implementa `Rescauta.Domain.Common.IAggregateRoot`.
3. Propiedades con setter privado. El estado se cambia por metodos de negocio explicitos
   (`entidad.RegistrarEntrada(...)`), nunca con `entidad.Cantidad = 5`.
4. **Cero EF Core**: sin `[Column]`, sin `DbSet`, sin `IQueryable`, sin `virtual`, sin lazy loading.
5. **Cero dependencias de Infrastructure**: nada de `ICacheService` ni `IHubContext<RescautaHub>`.
6. Validar con `DomainException` (ver `Rescauta.Domain/Exceptions/DomainException.cs`).
7. Id siempre `Guid` generado en cliente (ver `Common/BaseEntity.cs`).
8. Nombres en espanol, sin tildes ni enye en tipos y miembros: evita problemas de
   normalizacion de archivos y colisiones al fusionar ramas de los distintos modulos.
