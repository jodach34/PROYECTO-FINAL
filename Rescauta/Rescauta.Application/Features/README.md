# Features - un slice vertical por modulo

Cada caso de uso vive en su propia carpeta dentro de `Features/<Modulo>/<Accion>/`:

```
Features/Mapas/RegistrarPuntoRescate/
    RegistrarPuntoRescateCommand.cs          record + IRequest<Resultado>
    RegistrarPuntoRescateCommandHandler.cs   IRequestHandler<Command, Resultado>
    RegistrarPontoRescateValidator.cs        AbstractValidator<Command>
    RegistrarPuntoRescateDto.cs              request / response
```

## Que hay que hacer y que NO

1. **No registrar nada a mano.** `Application/DependencyInjection.cs` escanea este
   ensamblado con MediatR y FluentValidation. Crear la clase alcanza.
2. **No usar `IHubContext<RescautaHub>` ni `IDistributedCache`.** Inyectar
   `IRescautaNotifier` e `ICacheService` (contratos en `Application/Interfaces`).
3. **No tocar `AppDbContext`.** Resolver el agregado con `_dbContext.Set<TEntidad>()`.
4. **No referenciar `Rescauta.Infrastructure` ni `Rescauta.Api`.** No compila, y es a proposito.
5. Un handler por caso de uso. Si un handler supera ~100 lineas, sobra logica de negocio
   adentro: esa logica va en `Domain`.
6. Errores esperables: devolver `Result<T>.Failure(codigo, mensaje)`. Excepciones solo
   para casos que de verdad no deberian ocurrir.
