# Modulo Donaciones

Dueño de este módulo: **(asignar)**. Nadie mas escribe dentro de esta carpeta ni en
`Domain/Entities/Donaciones`, `Features/Donaciones` o `Controllers/v1/DonacionesController.cs`.

## Carpetas reservadas

```
Domain/Entities/Donaciones/                            Donante, Donacion, EstadoDonacion
Application/Features/Donaciones/                       casos de uso
Infrastructure/Persistence/Configurations/Donaciones/  mapeo EF Core
Infrastructure/Persistence/Repositories/Donaciones/    repositorios
Api/Controllers/v1/DonacionesController.cs             endpoints
```

## Alcance sugerido

- Registro de donaciones y donors.
- Estados de una donacion (recibida, clasificada, asignada, entregada).
- Conversion de donaciones en entradas de inventario: **este es el punto de contacto
  con Kardex**, y por lo tanto el unico lugar del sistema con riesgo de conflicto real
  entre los dos devs.

## Regla del modulo (acordar antes de codear)

El cruce Donaciones <-> Kardex se resuelve con **contratos, no con llamadas directas**:

1. Donaciones define un evento de dominio, por ejemplo `DonacionConfirmada`.
2. Un handler de Application lo escucha y escribe el movimiento de inventario.
3. Ningun modulo importa entidades ni repositorios del otro. Se comunican por evento.

Si alguno de los dos devs necesita algo del otro y no puede resolverlo con un evento,
escalar antes de editar el código del otro módulo: el costo de una dependencia circular
entre Kardex y Donaciones es mucho mayor que el de un par de dias de retraso.

## Eventos de SignalR

Prefijo obligatorio: `Donaciones_` (cliente -> servidor) y `donaciones.` (servidor -> cliente).

## Claves de cache

Prefijo obligatorio: `rescauta:donaciones:`
