using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Common;

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

    private void ConfigureConventions(ModelBuilder modelBuilder)
    {
        // Esquema por defecto SOLO en proveedores que lo soportan.
        //
        // "rescauta" es un namespace real en PostgreSQL y en SQL Server, asi que ahi aporta:
        // aisla las tablas del proyecto de las de otras que compartan base. En SQLite NO
        // existe el concepto: todo cuelga de "main". Declararlo ahi no hacia falta nada y
        // EF Core lo descartaba avisando por cada entidad en cada arranque ("'Comedor' is
        // configured to use schema 'rescauta', but SQLite does not support schemas"), con el
        // ruido de cuatro warnings que hay que aprender a ignorar.
        //
        // El nombre del proveedor se lee de las opciones YA configuradas, no de IConfiguration,
        // para que el modelo dependa solo de como se registro el DbContext.
        if (!Database.IsSqlite())
        {
            modelBuilder.HasDefaultSchema("rescauta");
        }

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
