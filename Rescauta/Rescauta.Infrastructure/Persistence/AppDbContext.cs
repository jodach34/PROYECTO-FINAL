using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Common;
using Rescauta.Infrastructure.Persistence.Converters;

namespace Rescauta.Infrastructure.Persistence;

/// <summary>
/// Contexto de datos unico de Rescauta. Es la unica clase del proyecto que conoce
/// entidades concretas de EF Core.
///
/// Convencion para que los 3 modulos no se pisen:
///   * NO agregar un DbSet por entidad en esta clase. Usar <see cref="Set{TEntity}"/>.
///   * La configuracion de mapeo va en Persistence/Configurations/&lt;Modulo&gt;/IEntidadConfig.cs
///     y se aplica desde <see cref="ConfigureConventions"/> + ApplyConfigurationsFromAssembly.
///   * Las migraciones se generan UNA vez, desde la rama principal, por la persona
///     designada como "dueña del esquema".
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Implementacion explicita: <see cref="DbContext.Set{TEntity}()"/> no declara la
    /// restriccion <c>: BaseEntity</c> que si exige <see cref="IAppDbContext.Set{TEntity}"/>.
    /// La restriccion no se repite aqui porque se hereda del metodo base.
    /// </summary>
    DbSet<TEntity> IAppDbContext.Set<TEntity>() => Set<TEntity>();

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var wasClosed = Database.GetDbConnection().State != ConnectionState.Open;

            if (wasClosed)
            {
                await Database.OpenConnectionAsync(cancellationToken);
            }

            await using var command = Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;

            await command.ExecuteScalarAsync(cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Global y por preconvencion: SQLite no ordena por DateTimeOffset y el fallo
        // sale como NotSupportedException al traducir la consulta. Guardar en UTC
        // resuelve los tres proveedores de una vez, sin tocar entidad por entidad.
        // Ver Persistence/Converters/UtcDateTimeOffsetConverter.cs.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<UtcDateTimeOffsetNullableConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica automaticamente TODAS las clases IEntityTypeConfiguration del ensamblado.
        // Un dev que agrega su IConfig en su carpeta de modulo no edita este archivo:
        // esto es lo que elimina los conflictos de merge en AppDbContext.cs.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Convenciones transversales para las 3 capas.
        ConfigureConventions(modelBuilder);
    }

    private static void ConfigureConventions(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rescauta");

        // Filtro global de borrado logico. EF Core 8 no trae soft delete nativo
        // (llego recien en EF Core 10), asi que se aplica con SetQueryFilter sobre
        // cualquier entidad que herede de BaseEntity. Efecto: ningun modulo puede
        // "olvidar" el filtro y leer registros borrados logicamente.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(
                Expression.Convert(parameter, typeof(BaseEntity)),
                nameof(BaseEntity.IsDeleted));

            entityType.SetQueryFilter(Expression.Lambda(Expression.Not(isDeleted), parameter));
        }
    }
}
