using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rescauta.Domain.Entities;
using Rescauta.Domain.ValueObjects;

namespace Rescauta.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="Comedor"/>.
///
/// Los atributos no van en la entidad: el Domain no referencia EF Core. Todo el mapeo
/// vive aqui, y <c>AppDbContext.OnModelCreating</c> lo descubre solo con
/// ApplyConfigurationsFromAssembly, asi que agregar una entidad no obliga a tocar el
/// DbContext (y con el, a pelear el archivo con los otros dos devs).
/// </summary>
public sealed class ComedorConfiguration : IEntityTypeConfiguration<Comedor>
{
    public void Configure(EntityTypeBuilder<Comedor> builder)
    {
        builder.ToTable("comedores");
        builder.HasKey(comedor => comedor.Id);

        builder.Property(comedor => comedor.Nombre)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(comedor => comedor.Distrito)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(comedor => comedor.Direccion)
            .HasMaxLength(240)
            .IsRequired();

        builder.Property(comedor => comedor.ContactoDirecto)
            .HasMaxLength(40);

        builder.Property(comedor => comedor.RacionesDiarias)
            .IsRequired();

        // El enum se guarda como texto y no como entero. Cambiar el orden de los
        // valores en el .cs no debe cambiar lo que hay en la base: con string, el dato
        // sobrevive al redeploy y el reporte sigue siendo legible.
        builder.Property(comedor => comedor.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // La API expone el enum como texto en el JSON (System.Text.Json serializa enums
        // como string por el JsonStringEnumConverter de Program.cs), y la base tambien.
        builder.HasIndex(comedor => comedor.Estado)
            .HasDatabaseName("ix_comedores_estado");

        builder.HasIndex(comedor => comedor.Distrito)
            .HasDatabaseName("ix_comedores_distrito");

        builder.Property(comedor => comedor.Ubicacion)
            .HasConversion(UbicacionGeoConverter())
            .HasColumnName("ubicacion")
            .HasMaxLength(48);

        // Comedor -> Insumo. Sin cascade: el borrado de un comedor se hace por logica
        // (IsDeleted) y sus inventarios deben sobrevivir para la auditoria del kardex.
        builder.HasMany<Insumo>()
            .WithOne()
            .HasForeignKey(insumo => insumo.ComedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Convierte <see cref="UbicacionGeo"/> a una sola columna de texto "lat,lon".
    ///
    /// Se eligio una columna y no dos porque SQLite no tiene tipo geometrico, y
    /// Postgres y SQL Server tienen cada uno el suyo: una columna de texto funciona
    /// igual en los tres proveedores sin ramificar el modelo.
    ///
    /// LIMITACION CONOCIDA: con este mapeo no se puede filtrar por proximity con SQL
    /// ("comedores dentro de 2 km"). Si el modulo de mapas necesita consultas
    /// espaciales, la salida es promover la columna a PostGIS (geography) y cambiar solo
    /// esta clase; el Domain no se toca.
    /// </summary>
    private static ValueConverter<UbicacionGeo?, string?> UbicacionGeoConverter() =>
        new(
            valor => Serializar(valor),
            texto => UbicacionGeoDesdeTexto(texto));

    /// <summary>
    /// Escribe el value object como "lat,lon". Vive en un metodo aparte porque un
    /// expression tree no admite el operador de patron <c>is null</c>: todo lo que no
    /// sea una llamada a metodo tiene que quedar fuera de la lambda del converter.
    /// </summary>
    private static string? Serializar(UbicacionGeo? valor) =>
        valor is null ? null : valor.ToString();

    /// <summary>
    /// Reconstruye el value object desde la columna de texto.
    ///
    /// Devuelve null en vez de lanzar cuando el dato esta corrupto: una fila con la
    /// ubicacion danada no debe impedir que se listen los demas comedores del mapa. El
    /// pin simplemente no aparece. La razon por la que se perdio el dato se detecta con
    /// una consulta de integridad, no botanando la peticion entera.
    ///
    /// El parseo es con InvariantCulture a proposito: la base guarda siempre punto
    /// decimal, y con la configuracion regional de un servidor en es-ES el "12.5" se
    /// leeria como "125".
    /// </summary>
    private static UbicacionGeo? UbicacionGeoDesdeTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var partes = texto.Split(',');

        if (partes.Length != 2
            || !double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitud)
            || !double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitud))
        {
            return null;
        }

        return new UbicacionGeo(latitud, longitud);
    }
}
