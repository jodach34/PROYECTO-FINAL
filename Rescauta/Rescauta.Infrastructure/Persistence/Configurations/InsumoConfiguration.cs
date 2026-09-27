using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities;

namespace Rescauta.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="Insumo"/>.
///
/// La columna de stock se mapea con alta precision decimal (18,3): los tipos default
/// de EF Core para <c>decimal</c> en SQLite y SQL Server pueden truncar a 2 decimales
/// o provocar overflow, y un inventario que redondea a proposito no sirve.
/// </summary>
public sealed class InsumoConfiguration : IEntityTypeConfiguration<Insumo>
{
    public void Configure(EntityTypeBuilder<Insumo> builder)
    {
        builder.ToTable("insumos");
        builder.HasKey(insumo => insumo.Id);

        builder.Property(insumo => insumo.Nombre)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(insumo => insumo.Categoria)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(insumo => insumo.UnidadMedida)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(insumo => insumo.StockActual)
            .HasPrecision(18, 3);

        builder.Property(insumo => insumo.StockMinimo)
            .HasPrecision(18, 3);

        builder.Property(insumo => insumo.ConsumoDiarioEstimado)
            .HasPrecision(18, 3);

        // Indice compuesto: el panel siempre consulta "los insumos de este comedor".
        builder.HasIndex(insumo => new { insumo.ComedorId, insumo.Nombre })
            .HasDatabaseName("ix_insumos_comedor_nombre");

        // El filtro de "insumos criticos" (stock <= minimo) es el mas frecuente del
        // sistema, asi que su indice se declara explicito.
        builder.HasIndex(insumo => insumo.StockActual)
            .HasDatabaseName("ix_insumos_stock_actual");
    }
}
