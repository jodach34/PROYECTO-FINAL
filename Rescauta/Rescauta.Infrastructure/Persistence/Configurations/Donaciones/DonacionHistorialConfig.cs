using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Donaciones;

namespace Rescauta.Infrastructure.Persistence.Configurations.Donaciones;

/// <summary>
/// Mapeo del historial de estados de una donacion. Es una tabla append-only: el servicio
/// solo inserta filas, nunca actualiza ni borra. El indice compuesto sirve la linea de
/// tiempo de una donacion concreta sin ordenar en memoria.
/// </summary>
public sealed class DonacionHistorialConfig : IEntityTypeConfiguration<DonacionHistorial>
{
    public void Configure(EntityTypeBuilder<DonacionHistorial> builder)
    {
        builder.ToTable("donaciones_historial");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.DonacionId)
            .IsRequired();

        builder.Property(h => h.EstadoAnterior)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.EstadoNuevo)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.Motivo)
            .HasMaxLength(DonacionHistorial.MotivoMaxLength);

        builder.Property(h => h.Fecha)
            .IsRequired();

        builder.Property(h => h.RowVersion)
            .IsConcurrencyToken();

        builder.HasOne<Donacion>()
            .WithMany()
            .HasForeignKey(h => h.DonacionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.DonacionId, h.Fecha })
            .HasDatabaseName("ix_donaciones_historial_donacion_fecha");
    }
}
