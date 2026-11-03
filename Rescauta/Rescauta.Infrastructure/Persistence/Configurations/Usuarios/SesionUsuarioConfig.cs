using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Persistence.Configurations.Usuarios;

public sealed class SesionUsuarioConfig : IEntityTypeConfiguration<SesionUsuario>
{
    public void Configure(EntityTypeBuilder<SesionUsuario> builder)
    {
        builder.ToTable("sesiones_usuario");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TokenHash)
            .IsRequired()
            .HasMaxLength(SesionUsuario.TokenHashMaxLength);

        builder.Property(s => s.Ip)
            .HasMaxLength(SesionUsuario.IpMaxLength);

        builder.Property(s => s.UserAgent)
            .HasMaxLength(SesionUsuario.UserAgentMaxLength);

        builder.Property(s => s.RowVersion)
            .IsConcurrencyToken();

        builder.HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_sesiones_usuario_token");

        builder.HasIndex(s => s.UsuarioId)
            .HasDatabaseName("ix_sesiones_usuario_usuario");

        builder.HasIndex(s => s.ExpiraAt)
            .HasDatabaseName("ix_sesiones_usuario_expira");
    }
}
