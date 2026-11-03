using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Persistence.Configurations.Usuarios;

public sealed class SolicitudOperadorConfig : IEntityTypeConfiguration<SolicitudOperador>
{
    public void Configure(EntityTypeBuilder<SolicitudOperador> builder)
    {
        builder.ToTable("solicitudes_operador");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Organizacion)
            .IsRequired()
            .HasMaxLength(Usuario.OrganizacionMaxLength);

        builder.Property(s => s.Mensaje)
            .HasMaxLength(SolicitudOperador.MensajeMaxLength);

        builder.Property(s => s.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.MotivoRechazo)
            .HasMaxLength(500);

        builder.Property(s => s.RowVersion)
            .IsConcurrencyToken();

        builder.HasIndex(s => s.UsuarioId)
            .HasDatabaseName("ix_solicitudes_operador_usuario");

        builder.HasIndex(s => s.Estado)
            .HasDatabaseName("ix_solicitudes_operador_estado");
    }
}
