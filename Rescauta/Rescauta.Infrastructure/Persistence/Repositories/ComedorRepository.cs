using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Repositories;

namespace Rescauta.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion EF Core de <see cref="IComedorRepository"/>.
///
/// Usa <c>IAppDbContext.Set&lt;T&gt;()</c> y no <c>AppDbContext</c> directo: asi este
/// archivo depende de la abstraccion y no de EF Core, y el DbContext se puede
/// sustituir en las pruebas sin tocar el repositorio.
///
/// El filtro global de borrado logico lo aplica <c>AppDbContext</c>, asi que ninguna
/// consulta de aqui necesita escribir <c>Where(c =&gt; !c.IsDeleted)</c> a mano.
/// </summary>
public sealed class ComedorRepository : IComedorRepository
{
    private readonly IAppDbContext _dbContext;

    public ComedorRepository(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Comedor?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Set<Comedor>()
            .FirstOrDefaultAsync(comedor => comedor.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Comedor>> ListarAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Comedor>()
            .AsNoTracking()
            .OrderBy(comedor => comedor.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Comedor>> ListarPorFiltrosAsync(
        string? nombre = null,
        EstadoAbastecimiento? estado = null,
        string? distrito = null,
        CancellationToken cancellationToken = default)
    {
        // Los filtros se acumulan con AND. Condicionar en el propio LINQ mantiene todo
        // en una sola consulta a la base, en vez de traer la tabla entera y filtrar en
        // memoria, que es lo que haria un IQueryable mal acotado.
        var consulta = _dbContext.Set<Comedor>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            var texto = nombre.Trim();
            consulta = consulta.Where(comedor => EF.Functions.Like(comedor.Nombre, $"%{texto}%"));
        }

        if (estado.HasValue)
        {
            consulta = consulta.Where(comedor => comedor.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(distrito))
        {
            var texto = distrito.Trim();
            consulta = consulta.Where(comedor => comedor.Distrito == texto);
        }

        return await consulta
            .OrderBy(comedor => comedor.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comedor>> ListarPorUrgenciaAsync(
        CancellationToken cancellationToken = default) =>
        // El enum esta mapeado como string, pero se sigue podendo ordenar por su valor
        // numerico: EF Core traduce la comparacion sobre el valor, no sobre el texto.
        await _dbContext.Set<Comedor>()
            .AsNoTracking()
            .OrderByDescending(comedor => comedor.Estado)
            .ThenBy(comedor => comedor.Nombre)
            .ToListAsync(cancellationToken);

    public async Task AgregarAsync(Comedor comedor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comedor);

        await _dbContext.Set<Comedor>().AddAsync(comedor, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Actualizar(Comedor comedor)
    {
        ArgumentNullException.ThrowIfNull(comedor);

        _dbContext.Set<Comedor>().Update(comedor);
    }
}
