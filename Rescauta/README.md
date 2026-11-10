# Rescauta - Logistica Comunitaria

Cascarón (shell) de la solución. **No contiene lógica de negocio**: solo la
infraestructura y los puntos de extensión para que 3 desarrolladores trabajen en
paralelo sobre los módulos **Mapas**, **Kardex/Inventario** y **Donaciones** sin pisarse.

- Runtime objetivo: **.NET 8 (LTS)**
- Base de datos: **SQLite** en desarrollo, prepared para **SQL Server** y **PostgreSQL**
- Cache distribuida: **Redis** (StackExchange.Redis)
- Tiempo real: **SignalR** (WebSockets)

---

## 1. Puesta en marcha (5 minutos)

```bash
git clone <url-del-repo>
cd Rescauta

# Redis local (opcional en desarrollo: la API arranca sin el, degradando a "sin cache")
docker compose up -d

# Compilar
dotnet restore
dotnet build

# Levantar la API
dotnet run --project Rescauta.Api
```

Verificar que todo quedó cableado:

| Qué | Endpoint | Esperado |
|---|---|---|
| La API arrancó | `http://localhost:5266/api/v1/system/info` | 200 |
| Base de datos | `http://localhost:5266/api/v1/system/readiness` | `databaseAvailable: true` |
| Redis | mismo endpoint | `cacheAvailable: true` (o `false` si no levantaste Redis) |
| Señal | `http://localhost:5266/health` | 200 `Degraded` si falta Redis |
| WebSocket | `ws://localhost:5266/hubs/rescauta` | handshake OK |
| Swagger | `http://localhost:5266/swagger` | UI de .NET 8 |

`rescauta.dev.db` (SQLite) se crea solo en el primer arranque.

### Si `dotnet run` falla con "You must install or update .NET"

El SDK 10 compila bien `net8.0`, pero **para ejecutar** hace falta el runtime
ASP.NET Core 8 (el de .NET Core 8 solo no alcanza):

```bash
dotnet --list-runtimes | findstr AspNetCore
```

Si no aparece `Microsoft.AspNetCore.App 8.x`, instalar el ASP.NET Core Runtime 8.
Alternativa: levantar el proyecto con `RollForward` o cambiar el TFM a `net10.0` en
`Directory.Build.props` (único lugar donde está el `TargetFramework`).

---

## 2. Estructura de la solución

```
Rescauta/
├── Rescauta.sln
├── Directory.Build.props          # TargetFramework net8.0 y reglas comunes
├── Directory.Packages.props       # VERSIONES de NuGet centralizadas (editar acá)
├── docker-compose.yml             # Redis (y Postgres/SqlServer bajo perfil)
│
├── Rescauta.Domain/               # CAPA 1 - Nucleo del negocio. Cero dependencias.
│   ├── Common/                    #   BaseEntity, IAggregateRoot, DomainEvent
│   ├── Entities/{Mapas,Kardex,Donaciones,Compartido}/
│   ├── ValueObjects/              #   Base de value objects
│   ├── Enums/                     #   Enums del negocio
│   ├── Events/                    #   Eventos de dominio
│   ├── Exceptions/                #   DomainException
│   └── Interfaces/                #   Abstracciones del dominio
│
├── Rescauta.Application/          # CAPA 2 - Casos de uso. Ref: SOLO Domain.
│   ├── Common/                    #   Result<T>, PagedResult<T>
│   ├── Interfaces/                #   IAppDbContext, ICacheService, IRescautaNotifier
│   ├── Features/{Mapas,Kardex,Donaciones}/   # UN SLICE POR MODULO (ver abajo)
│   ├── Services/                  #   Servicios de aplicacion
│   └── DependencyInjection.cs     #   AddApplicationServices()
│
├── Rescauta.Infrastructure/       # CAPA 3 - Adaptadores. Ref: Domain + Application.
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/        #   IEntityTypeConfiguration por entidad
│   │   ├── Interceptors/          #   Auditoria automatica
│   │   ├── Migrations/            #   Generadas por EF Core
│   │   └── Repositories/
│   ├── Caching/                   #   RedisCacheService
│   ├── Hubs/RescautaHub.cs        #   SignalR
│   ├── RealTime/                  #   Notificador hacia SignalR
│   ├── Options/                   #   DatabaseOptions
│   ├── Extensions/                #   Migraciones al arrancar
│   └── DependencyInjection.cs     #   AddInfrastructure()
│
└── Rescauta.Api/                  # CAPA 4 - HTTP + WebSocket. Composition root.
    ├── Program.cs                 #   El unico archivo que el equipo no deberia tocar
    ├── Controllers/v1/            #   Un controller por modulo
    ├── HealthChecks/              #   Checks de base y cache
    ├── Middleware/                #   Traductor de excepciones
    ├── appsettings.json
    └── appsettings.Development.json
```

### Reglas de dependencia (las rompe el compilador, no la revisión)

```
Api  ->  Application  ->  Domain
 |                      ^
 +------>  Infrastructure  --|
```

- `Domain` no referencia nada. Ni EF Core, ni Redis, ni MediatR.
- `Application` no referencia `Infrastructure`. Usa interfaces propias
  (`IAppDbContext`, `ICacheService`, `IRescautaNotifier`).
- Solo `Infrastructure` conoce EF Core, Redis, SignalR y los proveedores de BD.
- Solo `Api` es composition root y conoce `Program.cs`, CORS, mapeo de hubs.

---

## 3. Protocolo para trabajar los 3 módulos en paralelo

Este es el punto crítico. Las reglas existen para que **nadie tenga que editar el mismo
archivo que otro**.

### 3.1 Un slice vertical por módulo

Cada dev crea su carpeta y **solo escribe dentro de ella**:

```
Rescauta.Application/Features/Mapas/RegistrarPuntoRescate/
    RegistrarPuntoRescateCommand.cs        # IRequest + record
    RegistrarPuntoRescateCommandHandler.cs # IRequestHandler
    RegistrarPuntoRescateValidator.cs      # AbstractValidator
    RegistrarPuntoRescateDto.cs            # DTOs de entrada/salida
```

Mismo patron en `Features/Kardex/` y `Features/Donaciones/`.

**Nada se registra a mano**: `AddApplicationServices()` escanea el ensamblado con
MediatR y FluentValidation. Crear la clase es suficiente.

### 3.2 Quién escribe en qué archivo compartido

| Archivo | Puede editarlo | Nota |
|---|---|---|
| `Program.cs` | solo el integrator | Sigue siendo valido tal cual para los 3 módulos |
| `AppDbContext.cs` | **nadie** | Usar `Set<T>()`. No agregar `DbSet` por entidad |
| `AppDbContext.cs` (convenciones) | **nadie** | El filtro global de borrado ya esta |
| `Infrastructure/DependencyInjection.cs` | solo el integrator | Solo si se registra un servicio transversal |
| `Application/DependencyInjection.cs` | solo el integrator | Un servicio propio se registra con `TryAddScoped` desde su modulo |
| `RescautaHub.cs` | **nadie** | Agregar metodos con prefijo de modulo: `Mapas_...`, `Kardex_...`, `Donaciones_...` |
| `Directory.Packages.props` | con acuerdo del equipo | Versiones centralizadas |

### 3.3 Configuración de EF Core por entidad

Nunca editar `AppDbContext`. Crear la configuración en la carpeta del módulo:

```csharp
// Rescauta.Infrastructure/Persistence/Configurations/Kardex/ArticuloConfig.cs
public sealed class ArticuloConfig : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> builder)
    {
        builder.ToTable("articulos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Codigo).HasMaxLength(32).IsRequired();
        builder.HasIndex(a => a.Codigo).IsUnique();
    }
}
```

`AppDbContext` la descubre sola con `ApplyConfigurationsFromAssembly`.

### 3.4 Migraciones: un solo dueño

Generar migraciones en paralelo produce migraciones rotas al integrarlas. Flujo acordado:

1. Cada dev genera la suya en su rama, con prefijo de módulo:
   ```bash
   dotnet ef migrations add Kardex_InitialCreate \
     -p Rescauta.Infrastructure -s Rescauta.Api -o Persistence/Migrations
   ```
2. PR contra `main`. La persona **dueña del esquema** revisa y aprueba.
3. `main` aplica las migraciones al desplegar. En desarrollo se aplican solas al arrancar.

Herramienta global (una vez por maquina):

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

### 3.5 Cache: prefijo por módulo

```
rescauta:{modulo}:{entidad}:{id|slug}:{version}
rescauta:mapas:puntos:{guid}:v1
rescauta:kardex:articulos:v1
rescauta:donaciones:resumen:v1
```

Al invalidar, usar siempre el prefijo del módulo (`RemoveByPrefixAsync`), nunca claves
sueltas: un módulo nunca debe borrar la cache de otro.

### 3.6 Cambiar de base de datos

Una línea, sin recompilar:

```jsonc
"Database": {
  "Provider": "Postgres",                                  // "Sqlite" | "SqlServer" | "Postgres"
  "ConnectionString": "Host=localhost;Database=rescauta;Username=rescauta;Password=***"
}
```

### 3.7 Secretos

Nunca credenciales en `appsettings.json`. Usar secretos de usuario:

```bash
dotnet user-secrets --project Rescauta.Api set "ConnectionStrings:Redis" "localhost:6379"
dotnet user-secrets --project Rescauta.Api set "Database:ConnectionString" "Data Source=rescauta.dev.db"
```

En produccion, variables de entorno: `Database__Provider`, `Database__ConnectionString`,
`ConnectionStrings__Redis`.

### 3.8 Variables de Render

Obligatorias para que el despliegue quede completo:

| Variable | Servicio | Para que |
|---|---|---|
| `Api__BaseUrl` | MVC | URL publica de la API, obligatoriamente con `https` y barra final. El MVC no arranca si falta, apunta a localhost o usa HTTP. |
| `Qr__SeguimientoBaseUrl` | API | URL publica del MVC que abre el seguimiento del QR. |
| `Auth__ResetPasswordUrl` | API | URL publica del MVC para restablecer contrasena. No puede ser localhost. |
| `Internal__ApiKey` | API y MVC | Mismo secreto en ambos, minimo 32 caracteres. Sin ella la API y el MVC no arrancan en produccion. Una clave distinta en el MVC hace que la API rechace sus solicitudes con 401. Nunca se registra en logs. |
| `Auth__SeedAdminEmail` | API | Correo del administrador inicial. |
| `Auth__SeedAdminPassword` | API | Contrasena del administrador inicial. Minimo 8 caracteres, con al menos una letra y un numero. No versionar. |

El administrador inicial se crea solo si no existe una cuenta con ese correo. Si en
produccion no hay ninguna cuenta administradora y no se configuran ambas variables, la
API no arranca. Una cuenta existente nunca se modifica automaticamente.

Los datos demo de `DatosSemilla` (comedores y movimientos ficticios) solo se siembran en
`Development`. En produccion no se crean datos de ejemplo.

Opcionales:

| Variable | Para que |
|---|---|
| `Proxy__TrustedNetworks` | CIDR de proxies inmediatos separados por coma. Si se omite, se confia en loopback y en redes privadas IPv4, ULA IPv6 y link-local IPv6. En Render se puede restringir al rango exacto del proxy. |
| `Email__Host`, `Email__Port`, `Email__EnableSsl`, `Email__User`, `Email__Password`, `Email__From`, `Email__FromName` | Envio de correos de recuperacion. Sin `Email__Host` el correo no se envia. |
| `Gemini__ApiKey` | Generacion de respuestas del chatbot. Sin ella, el chatbot queda no disponible. |
| `Qdrant__Url`, `Qdrant__ApiKey` | Busqueda de documentos del chatbot. Sin ellas, el chatbot queda no disponible. |
| `ConnectionStrings__Redis` | Cache distribuida. Si falta, la API usa cache en memoria. |

No se debe confiar en `X-Forwarded-For` para identificar al cliente: su primer valor lo
puede falsificar el cliente. La API usa `CF-Connecting-IP` cuando la conexion inmediata
es un proxy privado de Render, o `X-Rescauta-Client-Ip` cuando el MVC la reenvia con la
clave `Internal__ApiKey`. Las cabeceras con IP privada, loopback, link-local o multicast
se ignoran y se usa la conexion inmediata.

---

## 4. Convenciones transversales

| Tema | Regla |
|---|---|
| Idioma | Todo en español, **sin tildes ni ñ en nombres** de tipo y miembro (evita problemas de normalización de archivos y de merge) |
| Ids | `Guid` generado en cliente |
| Concurrencia | `RowVersion` (rowversion) en toda entidad |
| Borrado | Lógico vía `IsDeleted`; el filtro global ya está aplicado. Para leer borrados: `IgnoreQueryFilters()` |
| Auditoría | `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` los completa `AuditableEntityInterceptor` |
| Errores de negocio | Lanzar `DomainException`; el middleware la traduce a 400 |
| Excepciones | Nunca se capturan en controllers. `ExceptionHandlingMiddleware` las traduce |
| Respuestas de casos de uso | Devolver `Result<T>`, no lanzar excepción, para errores esperables |
| Health checks | `Unhealthy` solo si la app no puede servir; cache caída = `Degraded` |
| Redis caído | La app **no** se cae. `RedisCacheService` degrada a "sin cache" |
| Estilo | `.editorconfig` incluido. `dotnet format` antes de cada PR |

---

## 5. Qué NO tocar sin acuerdo del equipo

- `Program.cs`, `AppDbContext.cs`, `RescautaHub.cs` y los dos `DependencyInjection.cs`.
  Si un módulo necesita algo ahí, primero se resuelve con una interface + registro
  automático; si aun asi hay que tocarlo, es un PR del integrator.
- Migraciones generadas por otros.
- Versiones en `Directory.Packages.props` (cambio puntual, con aviso previo).

---

## 6. Comandos utiles

```bash
# Compilar todo
dotnet build Rescauta.sln

# Formatear (respeta .editorconfig)
dotnet format Rescauta.sln

# Pruebas (cuando exista el proyecto de tests)
dotnet test

# EF Core: crear migracion
dotnet ef migrations add <Modulo>_<Descripcion> -p Rescauta.Infrastructure -s Rescauta.Api -o Persistence/Migrations

# EF Core: aplicar migraciones a mano
dotnet ef database update -p Rescauta.Infrastructure -s Rescauta.Api

# EF Core: listar migraciones pendientes
dotnet ef migrations list -p Rescauta.Infrastructure -s Rescauta.Api
```

---

## 7. Estado actual

El cascarón compila (`dotnet build`, 0 errores / 0 advertencias) y la API arranca con
EF Core + Redis + SignalR cableados. **No hay entidades de negocio todavia**: el primer
módulo que se incorpore define el esquema y genera la migracion inicial.
