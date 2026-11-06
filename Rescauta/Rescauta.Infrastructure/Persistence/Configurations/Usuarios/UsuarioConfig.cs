using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Persistence.Configurations.Usuarios;

public sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nombre)
            .IsRequired()
            .HasMaxLength(Usuario.NombreMaxLength);

        builder.Property(u => u.Correo)
            .IsRequired()
            .HasMaxLength(Usuario.CorreoMaxLength);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(Usuario.PasswordHashMaxLength);

        builder.Property(u => u.Rol)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(u => u.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(24);

        builder.Property(u => u.Organizacion)
            .HasMaxLength(Usuario.OrganizacionMaxLength);

        builder.Property(u => u.MotivoRechazo)
            .HasMaxLength(500);

        builder.Property(u => u.RowVersion)
            .IsConcurrencyToken();

        builder.HasIndex(u => u.Correo)
            .IsUnique()
            .HasDatabaseName("ux_usuarios_correo");

        builder.HasIndex(u => u.Rol)
            .HasDatabaseName("ix_usuarios_rol");

        builder.HasIndex(u => u.Estado)
            .HasDatabaseName("ix_usuarios_estado");
    }
}
