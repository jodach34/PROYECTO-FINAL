using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Persistence.Configurations.Usuarios;

public sealed class AsignacionComedorConfig : IEntityTypeConfiguration<AsignacionComedor>
{
    public void Configure(EntityTypeBuilder<AsignacionComedor> builder)
    {
        builder.ToTable("asignaciones_comedor");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.RowVersion)
            .IsConcurrencyToken();

        builder.HasIndex(a => new { a.UsuarioId, a.ComedorId })
            .IsUnique()
            .HasDatabaseName("ux_asignaciones_usuario_comedor");

        builder.HasIndex(a => a.ComedorId)
            .HasDatabaseName("ix_asignaciones_comedor");
    }
}
