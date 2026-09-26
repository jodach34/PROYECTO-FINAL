using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Donaciones;

namespace Rescauta.Infrastructure.Persistence.Configurations.Donaciones;

/// <summary>
/// Mapeo de <see cref="Donacion"/>. Vive en la carpeta del modulo y AppDbContext la descubre
/// sola con ApplyConfigurationsFromAssembly, asi que el modulo Donaciones no edita el
/// context (ver Rescauta/README.md 3.3).
///
/// Decisiones y por que:
///   * Tabla "donaciones" en minuscula: convencion del proyecto.
///   * Estado como TEXTO y no como entero: el historico se audita a mano y "Entregada" se
///     lee mejor que "3". El coste (string mas grande que int) es irrelevante aqui.
///   * RowVersion marcado como IsConcurrencyToken y NO como IsRowVersion: SQLite no tiene
///     el tipo rowversion de SQL Server ni triggers que lo generen, asi que IsRowVersion
///     (que espera que la base lo rellene) dejaria la columna siempre NULL y la
///     concurrencia optimista no detectaria nada. Como token de concurrencia, EF Core exige
///     que la aplicacion lo propague en el UPDATE, que es el comportamiento correcto.
/// </summary>
public sealed class DonacionConfig : IEntityTypeConfiguration<Donacion>
{
    public void Configure(EntityTypeBuilder<Donacion> builder)
    {
        builder.ToTable("donaciones");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.InsumoId)
            .IsRequired()
            .HasComment("Insumo donado. Referencia al modulo Kardex, sin FK todavia.");

        builder.Property(d => d.Cantidad)
            .IsRequired();

        builder.Property(d => d.FechaDonacion)
            .IsRequired();

        builder.Property(d => d.Donante)
            .IsRequired()
            .HasMaxLength(Donacion.DonanteMaxLength);

        builder.Property(d => d.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.CodigoSeguimiento)
            .IsRequired()
            .HasMaxLength(Donacion.CodigoSeguimientoMaxLength);

        // Token de concurrencia ver la nota del resumen de la clase.
        builder.Property(d => d.RowVersion)
            .IsConcurrencyToken();

        // El indice unico del codigo es la garantia de negocio mas importante: es lo que
        // impide registrar dos veces la misma donacion.
        builder.HasIndex(d => d.CodigoSeguimiento)
            .IsUnique()
            .HasDatabaseName("ix_donaciones_codigo_seguimiento");

        // Indices de lectura: el kardex consulta por insumo, y los listados por fecha.
        builder.HasIndex(d => d.InsumoId)
            .HasDatabaseName("ix_donaciones_insumo_id");

        builder.HasIndex(d => d.FechaDonacion)
            .HasDatabaseName("ix_donaciones_fecha");

        builder.HasIndex(d => d.Estado)
            .HasDatabaseName("ix_donaciones_estado");
    }
}
