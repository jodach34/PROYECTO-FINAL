using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Repositories;

namespace Rescauta.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion EF Core de <see cref="IDonacionRepository"/>.
///
/// <see cref="ListarComedoresRecomendadosAsync"/> no calcula distancia de verdad: sin
/// un servicio de geocodificacion y rutas, la ordenacion se hace por urgencia, que es
/// el criterio con el que el equipo decidio clasificar. La columna DistanciaKm del DTO
/// la completa la capa de casos de uso cuando exista el servicio de rutas.
/// </summary>
public sealed class DonacionRepository : IDonacionRepository
{
    private readonly IAppDbContext _dbContext;

    public DonacionRepository(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Donacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Set<Donacion>()
            .FirstOrDefaultAsync(donacion => donacion.Id == id, cancellationToken);

    public Task<Donacion?> ObtenerPorCodigoAsync(
        string codigoSeguimiento,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            return Task.FromResult<Donacion?>(null);
        }

        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();

        return _dbContext.Set<Donacion>()
            .FirstOrDefaultAsync(donacion => donacion.CodigoSeguimiento == codigo, cancellationToken);
    }

    public async Task<IReadOnlyList<Donacion>> ListarAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Donacion>()
            .AsNoTracking()
            .OrderByDescending(donacion => donacion.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Donacion>> ListarPorEstadoAsync(
        EstadoDonacion estado,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Donacion>()
            .AsNoTracking()
            .Where(donacion => donacion.Estado == estado)
            .OrderByDescending(donacion => donacion.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Comedor>> ListarComedoresRecomendadosAsync(
        CancellationToken cancellationToken = default) =>
        // Del mas urgente al mas abastecido. Cuando exista el servicio de rutas, el
        // desempate por distancia se anade aqui sin tocar la interfaz.
        await _dbContext.Set<Comedor>()
            .AsNoTracking()
            .OrderByDescending(comedor => comedor.Estado)
            .ThenBy(comedor => comedor.Nombre)
            .ToListAsync(cancellationToken);

    public Task<bool> ExisteCodigoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            return Task.FromResult(false);
        }

        var codigo = codigoSeguimiento.Trim().ToUpperInvariant();

        return _dbContext.Set<Donacion>()
            .AsNoTracking()
            .AnyAsync(donacion => donacion.CodigoSeguimiento == codigo, cancellationToken);
    }

    public async Task AgregarAsync(Donacion donacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(donacion);

        await _dbContext.Set<Donacion>().AddAsync(donacion, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Actualizar(Donacion donacion)
    {
        ArgumentNullException.ThrowIfNull(donacion);

        _dbContext.Set<Donacion>().Update(donacion);
    }
}
