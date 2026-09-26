using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rescauta.Api.HealthChecks;
using Rescauta.Api.Middleware;
using Rescauta.Application;
using Rescauta.Application.Interfaces.Caching;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Infrastructure;
using Rescauta.Infrastructure.Extensions;
using Rescauta.Infrastructure.Hubs;
using Rescauta.Infrastructure.Messaging;
using Rescauta.Infrastructure.Options;
using Rescauta.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// =============================================================================
// Rescauta - Logistica Comunitaria
// COMPOSITION ROOT UNICO. Este es el unico archivo que los devs deberian evitar tocar
// mientras desarrollan modulos. Ver Rescauta/README.md.
// =============================================================================

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// 0. Logging (Serilog: un solo formato de log para los 3 entornos de los devs).
//    Sustituye a los providers por defecto de Microsoft.Extensions.Logging.
// -----------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}    {Message:lj}{NewLine}{Exception}"));

// -----------------------------------------------------------------------------
// 1. Configuracion tipada de las secciones propias de infraestructura.
//    RabbitMq:Enabled en false deja la API sin mensajeria (ver AddInfrastructure).
// -----------------------------------------------------------------------------
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

// -----------------------------------------------------------------------------
// 2. Capa Application: casos de uso (MediatR), validacion (FluentValidation) y
//    registro de los servicios de Application. Todo se escanea por reflexion desde
//    el ensamblado, asi que un dev que crea un handler NO edita esta seccion.
// -----------------------------------------------------------------------------
builder.Services.AddApplicationServices();

// -----------------------------------------------------------------------------
// 3. Capa Infrastructure: EF Core (AppDbContext), Redis y los adaptadores de
//    Application. El proveedor de BD se elige con "Database:Provider".
// -----------------------------------------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);

// -----------------------------------------------------------------------------
// 4. Redis - cache distribuida.
//    AddStackExchangeRedisCache es idempotente: si Redis no levanta, la app igual
//    arranca y RedisCacheService degrada a "sin cache" (ver Infrastructure/Caching).
// -----------------------------------------------------------------------------
var redisConnection = builder.Configuration.GetConnectionString("Redis");

if (string.IsNullOrWhiteSpace(redisConnection))
{
    Log.Warning(
        "No se encontro ConnectionStrings:Redis. La API arranca SIN cache distribuida. " +
        "Levantar Redis con 'docker compose up -d' o definir la cadena de conexion.");
}
else
{
    redisConnection = NormalizeRedisConnectionString(redisConnection);
}

builder.Services.AddStackExchangeRedisCache(options =>
{
    var configuration = string.IsNullOrWhiteSpace(redisConnection) ? "localhost:6379" : redisConnection;

    // Ajustes de fail-fast. Sin esto, cada operacion de cache bloquea 5 segundos cuando
    // Redis no esta levantado, y /health y /readiness se vuelven inutilizables. Con
    // AbortOnConnectFail en false la app arranca igual y RedisCacheService degrada a
    // "sin cache", que es justo el comportamiento deseado en desarrollo.
    var redisOptions = ConfigurationOptions.Parse(configuration);
    redisOptions.AbortOnConnectFail = false;
    redisOptions.ConnectTimeout = Math.Min(redisOptions.ConnectTimeout, 1000);
    redisOptions.SyncTimeout = Math.Min(redisOptions.SyncTimeout, 1000);
    redisOptions.AsyncTimeout = Math.Min(redisOptions.AsyncTimeout, 1000);
    redisOptions.ConnectRetry = 0;

    options.ConfigurationOptions = redisOptions;
    options.InstanceName = builder.Configuration["Cache:InstanceName"] ?? "rescauta:";
});

// -----------------------------------------------------------------------------
// 5. SignalR - tiempo real (WebSockets / Server-Sent Events / Long Polling).
//    MapHub se registra mas abajo, en el pipeline, junto al resto del ruteo.
// -----------------------------------------------------------------------------
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("SignalR:KeepAliveIntervalInSeconds", 15));
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("SignalR:ClientTimeoutIntervalInSeconds", 30));
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// -----------------------------------------------------------------------------
// 6. API: controllers, versionado y Swagger.
// -----------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

var apiVersioning = builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"));
});

apiVersioning.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddResponseCompression();

// -----------------------------------------------------------------------------
// 7. Health checks: /health (infraestructura) sirve a los contenedores y a Docker.
// -----------------------------------------------------------------------------
// Los health checks reciben dependencias scoped (DbContext, cache). Se registran aqui
// porque viven en esta capa; AddCheck los resuelve creando un scope por ejecucion.
builder.Services.AddScoped<DatabaseHealthCheck>();
builder.Services.AddScoped<RedisHealthCheck>();

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, tags: new[] { "database" })
    .AddCheck<RedisHealthCheck>("cache", HealthStatus.Degraded, tags: new[] { "cache" });

// El check de AMQP se registra solo si la mensajeria esta habilitada: depende de
// RabbitMqConnection, que AddInfrastructure no registra cuando "RabbitMq:Enabled" es false.
// Registrarlo siempre haria fallar la resolucion de /health.
if (builder.Configuration.GetValue("RabbitMq:Enabled", true))
{
    builder.Services.AddScoped<RabbitMqHealthCheck>();
    healthChecksBuilder.AddCheck<RabbitMqHealthCheck>("messaging", HealthStatus.Degraded, tags: new[] { "messaging" });
}

// -----------------------------------------------------------------------------
// 8. CORS abierto para desarrollo.
//    En produccion cambiar por AllowCredentials + origenes explicitos:
//      builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
//          .WithOrigins("https://app.rescauta.org")
//          .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
// -----------------------------------------------------------------------------
const string CorsPolicyName = "RescautaCors";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (builder.Configuration.GetValue("Cors:AllowAnyOrigin", true))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

// -----------------------------------------------------------------------------
// 9. Middleware.
// -----------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Rescauta API v1"));
}

app.UseCors(CorsPolicyName);

app.UseResponseCompression();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

// -----------------------------------------------------------------------------
// 10. Ruteo de SignalR. Los devs de Mapas, Kardex y Donaciones reutilizan este mismo
//     hub: agregan sus metodos/clientes con prefijo de modulo, no un hub nuevo.
// -----------------------------------------------------------------------------
var hubRoute = builder.Configuration["SignalR:Route"] ?? RescautaHub.Route;
app.MapHub<RescautaHub>(hubRoute);

// -----------------------------------------------------------------------------
// 11. Migraciones automaticas en desarrollo. En produccion se ejecutan como paso
//     previo al despliegue, nunca al arrancar.
// -----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    await app.Services.ApplyPendingMigrationsAsync();
}

Log.Information("Rescauta API iniciada. Entorno: {Environment} | Hub: {HubRoute}", app.Environment.EnvironmentName, hubRoute);

await app.RunAsync();

// -----------------------------------------------------------------------------
// Helper de configuracion de Redis.
// -----------------------------------------------------------------------------
// Los paneles de Redis Cloud entregan la conexion como URI "redis://user:pass@host:puerto".
// PROBLEMA: ConfigurationOptions.Parse() NO entiende ese esquema y NO lanza excepcion:
// se traga la URI entera como nombre de host, deja el puerto en 0, la contrasena vacia y
// ssl en false. Como AbortOnConnectFail esta en false, la app arrancaba igual, sin cache y
// sin error visible. Esta funcion convierte la URI a sintaxis nativa de StackExchange.Redis
// para que ambos formatos sirvan:
//     redis://  -> ssl=false (puerto 6379 por defecto)
//     rediss:// -> ssl=true  (puerto 6380 por defecto, o el que venga en la URI)
// Un valor que ya venga en sintaxis nativa ("host:6379,user=..,password=..") se devuelve
// sin tocar, asi que este helper es seguro de aplicar siempre.
static string NormalizeRedisConnectionString(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("redis" or "rediss"))
    {
        return value;
    }

    var useSsl = uri.Scheme == "rediss";
    var port = uri.Port > 0 ? uri.Port : (useSsl ? 6380 : 6379);

    // UserInfo viene URL-encoded y con "usuario:contrasena" en un solo string.
    var user = string.Empty;
    var password = string.Empty;
    var separatorIndex = uri.UserInfo.IndexOf(':');

    if (separatorIndex >= 0)
    {
        user = Uri.UnescapeDataString(uri.UserInfo[..separatorIndex]);
        password = Uri.UnescapeDataString(uri.UserInfo[(separatorIndex + 1)..]);
    }
    else if (!string.IsNullOrEmpty(uri.UserInfo))
    {
        user = Uri.UnescapeDataString(uri.UserInfo);
    }

    var parts = new List<string> { $"{uri.Host}:{port}" };

    if (!string.IsNullOrEmpty(user))
    {
        parts.Add($"user={user}");
    }

    if (!string.IsNullOrEmpty(password))
    {
        parts.Add($"password={password}");
    }

    parts.Add($"ssl={(useSsl ? "true" : "false")}");

    var normalized = string.Join(",", parts);

    Log.Information(
        "ConnectionStrings:Redis estaba en formato URI; normalizado a sintaxis StackExchange.Redis (ssl={UseSsl}).",
        useSsl);

    return normalized;
}

/// <summary>
/// Marcador para que los tests de integracion (WebApplicationFactory) puedan referenciar
/// el assembly de la API. Reemplaza al <c>top-level statements</c> con un Program visible.
/// </summary>
public partial class Program;
