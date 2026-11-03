using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Persistence.Configurations.Usuarios;

public sealed class TokenRecuperacionConfig : IEntityTypeConfiguration<TokenRecuperacion>
{
    public void Configure(EntityTypeBuilder<TokenRecuperacion> builder)
    {
        builder.ToTable("tokens_recuperacion");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenRecuperacion.TokenHashMaxLength);

        builder.Property(t => t.RowVersion)
            .IsConcurrencyToken();

        builder.HasOne(t => t.Usuario)
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_tokens_recuperacion_token");

        builder.HasIndex(t => t.UsuarioId)
            .HasDatabaseName("ix_tokens_recuperacion_usuario");

        builder.HasIndex(t => t.ExpiraAt)
            .HasDatabaseName("ix_tokens_recuperacion_expira");
    }
}
