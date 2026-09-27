using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;

namespace Rescauta.Domain.Repositories;

/// <summary>
/// Contrato de acceso a datos de <see cref="Comedor"/>.
///
/// Vive en el Domain y no en Application a proposito: la abstraccion de persistencia
/// la posee el negocio, no la capa de casos de uso. Quien la implementa es
/// Infrastructure (EF Core), y ahi es donde aparece el unico <c>DbSet</c> del sistema.
///
/// Las firmas usan LINQ y <see cref="CancellationToken"/>, pero NO <c>IQueryable</c> ni
/// <c>DbSet</c>: filtrar en memoria desde el Domain mantiene la dependencia en una
/// sola direccion.
/// </summary>
public interface IComedorRepository
{
    Task<Comedor?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comedor>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Filtros del sidebar del mapa. Todos los parametros son opcionales y se
    /// combinan con AND: un filtro nulo no restringe.
    /// </summary>
    Task<IReadOnlyList<Comedor>> ListarPorFiltrosAsync(
        string? nombre = null,
        EstadoAbastecimiento? estado = null,
        string? distrito = null,
        CancellationToken cancellationToken = default);

    /// <summary>Comedores ordenados de mayor a menor urgencia. Alimenta el mapa.</summary>
    Task<IReadOnlyList<Comedor>> ListarPorUrgenciaAsync(CancellationToken cancellationToken = default);

    Task AgregarAsync(Comedor comedor, CancellationToken cancellationToken = default);

    void Actualizar(Comedor comedor);
}
