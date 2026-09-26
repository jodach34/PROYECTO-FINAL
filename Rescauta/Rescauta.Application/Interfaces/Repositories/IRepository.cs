using Rescauta.Domain.Common;

namespace Rescauta.Application.Interfaces.Repositories;

/// <summary>
/// Repositorio base de solo lectura/escritura, parametrizado por la raiz de agregado.
/// Complementa a <c>IAppDbContext.Set&lt;TEntity&gt;()</c> para los casos en que un modulo
/// necesite una consulta con nombre claro y reutilizable.
///
/// Concrete en su modulo:
///   Infrastructure/Persistence/Repositories/Mapas/PuntoRescateRepository.cs
///   Infrastructure/Persistence/Repositories/Kardex/ArticuloRepository.cs
///   Infrastructure/Persistence/Repositories/Donaciones/DonacionRepository.cs
/// </summary>
public interface IRepository<TEntity>
    where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Remove(TEntity entity);
}
