using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities;

namespace Rescauta.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="Donacion"/>.
///
/// El indice unico sobre CodigoSeguimiento es la ultima linea de defensa: la
/// aplicacion ya comprueba <c>ExisteCodigoAsync</c> antes de insertar, pero entre esa
/// comprobacion y el INSERT hay una ventana para dos donantes simultaneos. El indice
/// convierte esa carrera improbable en un error de base de datos en vez de en dos
/// QR con el mismo codigo, que si seria un problema real de trazabilidad.
/// </summary>
public sealed class DonacionConfiguration : IEntityTypeConfiguration<Donacion>
{
    public void Configure(EntityTypeBuilder<Donacion> builder)
    {
        builder.ToTable("donaciones");
        builder.HasKey(donacion => donacion.Id);

        builder.Property(donacion => donacion.CodigoSeguimiento)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(donacion => donacion.CodigoSeguimiento)
            .IsUnique()
            .HasDatabaseName("ux_donaciones_codigo_seguimiento");

        builder.Property(donacion => donacion.NombreProducto)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(donacion => donacion.Categoria)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(donacion => donacion.Cantidad)
            .HasPrecision(18, 3);

        builder.Property(donacion => donacion.UnidadMedida)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(donacion => donacion.DireccionRecojo)
            .HasMaxLength(240)
            .IsRequired();

        builder.Property(donacion => donacion.Estado)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(donacion => donacion.Estado)
            .HasDatabaseName("ix_donaciones_estado");

        // El comedor asignado se referencia sin cascade: al desasignar o anular una
        // donacion, el historial del comedor no puede desaparecer.
        builder.HasOne<Comedor>()
            .WithMany()
            .HasForeignKey(donacion => donacion.ComedorAsignadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
