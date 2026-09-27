using System.Reflection;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Comedores;
using Rescauta.Application.Features.Donaciones;
using Rescauta.Application.Features.Kardex;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Services;

namespace Rescauta.Application;

/// <summary>
/// Marcador de ensamblado. Se usa para el escaneo automatico de MediatR y de los
/// validadores de FluentValidation, asi los modulos solo tienen que crear sus handlers
/// dentro del proyecto: no hay que registrarlos a mano y no hay conflictos de merge.
/// </summary>
public static class ApplicationAssembly
{
    public static readonly Assembly Instance = typeof(ApplicationAssembly).Assembly;
}

public static class DependencyInjection
{
    /// <summary>
    /// Registra los casos de uso, validadores y abstracciones propias de Application.
    /// La API la invoca desde Program.cs mediante AddApplicationServices().
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = ApplicationAssembly.Instance;

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.TryAddScoped<ISystemReadinessService, SystemReadinessService>();

        // Modulos con logica de casos de uso propia. Se registran a mano (y no por
        // convencion como los handlers de MediatR) porque no son handlers: son servicios
        // con dependencias de Infrastructure, que MediatR no debe conocer.
        services.TryAddScoped<IComedorService, ComedorService>();
        services.TryAddScoped<IDonacionService, DonacionService>();
        services.TryAddScoped<IKardexService, KardexService>();

        // IKardexService se registro junto a los otros dos. Antes no estaba en ninguna parte
        // y el endpoint POST /api/v1/kardex/movimiento devolvia 500 con "Unable to resolve
        // service for type IKardexService": el modulo existed, el controller existia, pero el
        // caso de uso no se resolvia nunca, asi que el endpoint era inalcanzable. Estos
        // registros no se detectan solos porque los servicios no son handlers de MediatR.

        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }
}
