using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Rescauta.Infrastructure.Persistence.Converters;

/// <summary>
/// Convierte <see cref="DateTimeOffset"/> a <see cref="DateTime"/> en UTC para
/// guardarlo en la base.
///
/// POR QUE EXISTE: SQLite no soporta ordenar ni comparar columnas DateTimeOffset, y
/// falla con <c>NotSupportedException</c> en el momento de traducir la consulta, no al
/// escribir el dato. Por eso el bug aparece en los ORDER BY del kardex y en cuanto
/// alguien ordena por CreatedAt. DateTime si se soporta: EF lo guarda como texto ISO
/// 8601, que en SQLite ordena lexicograficamente y coincide con el orden cronologico.
///
/// QUE SE PIERDE: el offset original. A cambio, todos los valores quedan en UTC y las
/// comparaciones entre registros de distintos replicas son correctas, que es lo que
/// necesita un kardex. El offset se repone como cero al leer.
///
/// Se aplica como preconvencion en <c>AppDbContext.ConfigureConventions</c>, asi que
/// cubre las 4 entidades sin tocar ninguna.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTime>
{
    public UtcDateTimeOffsetConverter()
        : base(
            valor => valor.UtcDateTime,
            utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))
    {
    }
}

/// <summary>
/// Variante nullable de <see cref="UtcDateTimeOffsetConverter"/>.
///
/// Hace falta declararla aparte porque EF Core no aplica automaticamente una
/// preconvencion de <c>DateTimeOffset</c> a las propiedades <c>DateTimeOffset?</c>.
/// Sin este converter, <c>BaseEntity.UpdatedAt</c> fallaria igual que <c>CreatedAt</c>.
/// </summary>
public sealed class UtcDateTimeOffsetNullableConverter : ValueConverter<DateTimeOffset?, DateTime?>
{
    public UtcDateTimeOffsetNullableConverter()
        : base(
            valor => valor.HasValue ? valor.Value.UtcDateTime : null,
            utc => utc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc))
                : null)
    {
    }
}
