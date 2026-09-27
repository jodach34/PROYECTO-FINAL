using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Infrastructure.Persistence.Configurations.Kardex;

/// <summary>
/// Mapeo de <see cref="Insumo"/>.
///
/// Antes de este archivo el modulo Kardex se mapeaba solo por convencion y por eso no habia
/// ninguna config suya. Este NO es un remapeo completo: declara unicamente lo que la
/// convencion no puede saber, que es la FK al comedor y la precision del tope de stock.
/// Todo lo demas (clave, nombre, unidad, RowVersion) sigue saliendo de la convencion, para no
/// pisar el mapeo que el modulo ya tenia.
///
/// La FK se declara desde el lado del comedor (ComedorConfig), no aqui, para que la
/// relacion se defina en un solo lugar.
/// </summary>
public sealed class InsumoConfig : IEntityTypeConfiguration<Insumo>
{
    public void Configure(EntityTypeBuilder<Insumo> builder)
    {
        builder.HasMany(i => i.Movimientos)
            .WithOne()
            .HasForeignKey(m => m.InsumoId)
            .OnDelete(DeleteBehavior.Cascade);

        // La navegacion se lee por el campo privado _movimientos, no por la propiedad.
        // PropertyAccessMode.Field se lo dice de forma explicita: Insumo no expone una
        // coleccion escribible y EF, sin esta pista, no puede saber de donde sacar los
        // asientos que el dominio acaba de agregar.
        builder.Navigation(i => i.Movimientos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // La relacion con el comedor NO se declara aqui: es ComedorConfig la que la define,
        // con la navegacion que vive en la entidad Comedor. Declararla en los dos lados
        // hace que EF reclame que la configuracion se solape. Lo que si hace falta aqui es el
        // indice, porque el mapa y el panel filtran por comedor.
        builder.Property(i => i.StockMaximo)
            .HasPrecision(18, 3)
            .HasComment("Capacidad de la despensa. 0 = sin tope conocido.");

        builder.HasIndex(i => i.ComedorId)
            .HasDatabaseName("ix_insumos_comedor_id");
    }
}
