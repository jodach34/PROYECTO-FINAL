using System.Reflection;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rescauta.Application.Common;
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

        // Politica de negocio del panel: dias de stock critico. Scoped porque es
        // stateless y por si manana necesita leer configuracion por tenant.
        services.TryAddScoped<IUrgenciaCalculadorService, UrgenciaCalculadorService>();
    services.TryAddScoped<IKardexService, KardexService>();

        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }
}
