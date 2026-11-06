using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Rescauta.Domain.Common;

namespace Rescauta.Application.Interfaces;

/// <summary>
/// Abstraccion del contexto de datos. Los casos de uso de Application dependen de esta
/// interfaz, NUNCA de <c>Rescauta.Infrastructure.Persistence.AppDbContext</c>.
/// La implementacion real esta en Infrastructure/Persistence/AppDbContext.cs.
///
/// Se expone <c>Set&lt;TEntity&gt;()</c> en lugar de un <c>DbSet</c> por entidad a proposito:
/// asi cada modulo resuelve su agregado con <c>_dbContext.Set&lt;Donacion&gt;()</c> sin
/// tocar esta interfaz y sin generar conflictos de merge entre los 3 devs.
/// </summary>
public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>()
        where TEntity : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DatabaseFacade Database { get; }

    /// <summary>
    /// Comprueba que la base de datos responde con una consulta real ("SELECT 1").
    ///
    /// NO usar <c>Database.CanConnectAsync()</c> para esto: con SQLite devuelve false
    /// cuando el archivo todavia no existe, y en un clon nuevo recien clonado eso
    /// significa que la base jamas se crearia. Este Ping abre la conexion y ejecuta la
    /// consulta, asi que es fiable en los tres proveedores soportados.
    /// </summary>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
