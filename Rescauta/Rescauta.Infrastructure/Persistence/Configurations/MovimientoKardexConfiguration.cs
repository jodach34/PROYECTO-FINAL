using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities;

namespace Rescauta.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="MovimientoKardex"/>.
///
/// El indice compuesto (InsumoId, Fecha) no es decorativo: es lo que hace barato el
/// historial cronologico de un insumo, que es la consulta que se hace cada vez que se
/// recalcula el saldo. Sin el, cada recalculo seria un table scan del kardex entero.
///
/// La tabla no tiene columna de borrado por la misma razon append-only: el borrado
/// logico de BaseEntity se ignora a proposito, porque un movimiento no se borra nunca.
/// Aun asi se deja el filtro global intacto para que las consultas funcionen.
/// </summary>
public sealed class MovimientoKardexConfiguration : IEntityTypeConfiguration<MovimientoKardex>
{
    public void Configure(EntityTypeBuilder<MovimientoKardex> builder)
    {
        builder.ToTable("movimientos_kardex");
        builder.HasKey(movimiento => movimiento.Id);

        builder.Property(movimiento => movimiento.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(movimiento => movimiento.Cantidad)
            .HasPrecision(18, 3);

        builder.Property(movimiento => movimiento.StockResultante)
            .HasPrecision(18, 3);

        builder.Property(movimiento => movimiento.Referencia)
            .HasMaxLength(60);

        builder.Property(movimiento => movimiento.Responsable)
            .HasMaxLength(120);

        // EsEntrada es una propiedad calculada, no una columna. Sin esto EF Core
        // intentaria mapearla y la migracion fallaria.
        builder.Ignore(movimiento => movimiento.EsEntrada);

        builder.HasIndex(movimiento => new { movimiento.InsumoId, movimiento.Fecha })
            .HasDatabaseName("ix_movimientos_insumo_fecha");

        // El indice del kardex Reciente del panel ordena por fecha en todo el sistema.
        builder.HasIndex(movimiento => movimiento.Fecha)
            .HasDatabaseName("ix_movimientos_fecha");

        builder.HasOne<Insumo>()
            .WithMany()
            .HasForeignKey(movimiento => movimiento.InsumoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
