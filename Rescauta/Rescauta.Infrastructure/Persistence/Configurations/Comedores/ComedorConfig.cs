using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Comedores;

namespace Rescauta.Infrastructure.Persistence.Configurations.Comedores;

/// <summary>
/// Mapeo de <see cref="Comedor"/>. AppDbContext la descubre sola con
/// ApplyConfigurationsFromAssembly, asi que el modulo Comedores no edita el context.
///
/// Decisiones y por que:
///   * Tabla "comedores" en minuscula: convencion del proyecto.
///   * El indice por distrito es de lectura, no de unicidad: el mapa filtra por zona.
/// </summary>
public sealed class ComedorConfig : IEntityTypeConfiguration<Comedor>
{
    public void Configure(EntityTypeBuilder<Comedor> builder)
    {
        builder.ToTable("comedores");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(Comedor.NombreMaxLength);

        builder.Property(c => c.Distrito)
            .IsRequired()
            .HasMaxLength(Comedor.DistritoMaxLength);

        builder.Property(c => c.Direccion)
            .IsRequired()
            .HasMaxLength(Comedor.DireccionMaxLength);

        builder.Property(c => c.ContactoTelefono)
            .IsRequired()
            .HasMaxLength(Comedor.ContactoTelefonoMaxLength);

        builder.Property(c => c.RacionesDiarias)
            .IsRequired();

        // Porcentajes sobre el lienzo del mapa. Se guardan como decimal y no como float
        // porque el snapshot de EF los compara en las pruebas y float mete ruido binario.
        builder.Property(c => c.MapaX)
            .HasPrecision(5, 2)
            .HasComment("Posicion horizontal del pin en el mapa, 0-100.");

        builder.Property(c => c.MapaY)
            .HasPrecision(5, 2)
            .HasComment("Posicion vertical del pin en el mapa, 0-100.");

        // La relacion se declara desde el lado del comedor (WithMany) porque la navegacion
        // vive en la entidad. El comportamiento de borrado lo fija InsumoConfig, que es
        // donde esta la FK.
        builder.HasMany(c => c.Suministros)
            .WithOne()
            .HasForeignKey("ComedorId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Distrito)
            .HasDatabaseName("ix_comedores_distrito");

        builder.HasIndex(c => c.Nombre)
            .HasDatabaseName("ix_comedores_nombre");
    }
}
