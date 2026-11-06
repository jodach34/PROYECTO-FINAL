using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Comedores;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Infrastructure.Options;
using Rescauta.Infrastructure.Persistence;

namespace Rescauta.Infrastructure.Geocodificacion;

/// <summary>
/// Completa las coordenadas de los comedores que ya existian antes de habilitar la
/// geocodificacion. Procesa solo los que nunca lo intentaron, en lotes pequenos y con el
/// mismo rate limit que usa el alta, para no incumplir la politica de Nominatim.
///
/// No borra ni inventa nada: si una direccion no se encuentra, el comedor conserva
/// Latitud/Longitud en null y se marca el intento para no repetirlo en cada arranque.
/// </summary>
public sealed class GeocodificacionBackfillService(
    IServiceScopeFactory scopeFactory,
    IGeocodificador geocodificador,
    IOptions<GeocodificacionOptions> options,
    ILogger<GeocodificacionBackfillService> logger) : BackgroundService
{
    private readonly GeocodificacionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!geocodificador.EstaHabilitado)
        {
            logger.LogInformation(
                "Geocodificacion deshabilitada. No se buscan coordenadas para comedores existentes.");

            return;
        }

        try
        {
            // Deja terminar el arranque y la semilla antes de tocar la base.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var intervalo = TimeSpan.FromMinutes(Math.Max(1, _options.BackfillIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarPendientesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo el backfill de geocodificacion. Se reintentara en la proxima pasada.");
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcesarPendientesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendientes = await dbContext
            .Set<Comedor>()
            .Where(c => c.Latitud == null &&
                        c.Longitud == null &&
                        c.GeocodificacionIntentadaEn == null)
            .OrderBy(c => c.CreatedAt)
            .Take(Math.Clamp(_options.BackfillLote, 1, 100))
            .ToListAsync(cancellationToken);

        if (pendientes.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Geocodificando {Cantidad} comedor(es) existente(s) sin coordenadas.",
            pendientes.Count);

        foreach (var comedor in pendientes)
        {
            var coordenadas = await geocodificador.GeocodificarAsync(
                comedor.Direccion,
                comedor.Distrito,
                cancellationToken);

            if (coordenadas is { } ubicacion)
            {
                comedor.Ubicar(ubicacion.Latitud, ubicacion.Longitud);

                logger.LogInformation(
                    "Comedor geocodificado. Comedor={ComedorId}, Nombre={Nombre}, Latitud={Latitud}, Longitud={Longitud}",
                    comedor.Id,
                    comedor.Nombre,
                    comedor.Latitud,
                    comedor.Longitud);
            }
            else
            {
                comedor.MarcarGeocodificacionFallida();

                logger.LogWarning(
                    "No se pudo geocodificar el comedor {ComedorId} ({Nombre}). Se conserva sin coordenadas.",
                    comedor.Id,
                    comedor.Nombre);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
