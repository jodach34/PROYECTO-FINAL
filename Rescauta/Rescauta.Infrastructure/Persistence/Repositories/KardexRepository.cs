using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Services;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Repositories;

namespace Rescauta.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion EF Core de <see cref="IKardexRepository"/>.
///
/// Dos decisiones que conviene no deshacer sin discusion:
///
/// 1. <see cref="CalcularStockAsync"/> suma los movimientos en la base y NO lee
///    <c>Insumo.StockActual</c>. El saldo real es la suma de los movimientos; la
///    columna es solo una proyeccion para las consultas del mapa. Si divergen, gano la
///    suma, porque es la que tiene respaldo en el historial.
///
/// 2. No existe ningun metodo de update ni de delete para
///    <see cref="MovimientoKardex"/>. El kardex es append-only y la ausencia del metodo
///    en la interfaz es lo que lo hace cumplir a nivel de compilacion.
/// </summary>
public sealed class KardexRepository : IKardexRepository
{
    private readonly IAppDbContext _dbContext;
    private readonly IUrgenciaCalculadorService _urgenciaCalculador;

    public KardexRepository(IAppDbContext dbContext, IUrgenciaCalculadorService urgenciaCalculador)
    {
        _dbContext = dbContext;
        _urgenciaCalculador = urgenciaCalculador;
    }

    public Task<Insumo?> ObtenerInsumoAsync(Guid insumoId, CancellationToken cancellationToken = default) =>
        _dbContext.Set<Insumo>()
            .FirstOrDefaultAsync(insumo => insumo.Id == insumoId, cancellationToken);

    public async Task<IReadOnlyList<Insumo>> ListarInsumosPorComedorAsync(
        Guid comedorId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Insumo>()
            .AsNoTracking()
            .Where(insumo => insumo.ComedorId == comedorId)
            .OrderBy(insumo => insumo.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Insumo>> ListarInsumosCriticosAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.Set<Insumo>()
            .AsNoTracking()
            .Where(insumo => insumo.StockActual <= insumo.StockMinimo)
            .OrderBy(insumo => insumo.StockActual)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MovimientoKardex>> ListarMovimientosPorInsumoAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default) =>
        // El orden cronologico es parte del contrato declarado en IKardexRepository.
        await _dbContext.Set<MovimientoKardex>()
            .AsNoTracking()
            .Where(movimiento => movimiento.InsumoId == insumoId)
            .OrderBy(movimiento => movimiento.Fecha)
            .ThenBy(movimiento => movimiento.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MovimientoKardex>> ListarMovimientosRecientesAsync(
        int cantidad = 20,
        CancellationToken cancellationToken = default)
    {
        if (cantidad <= 0)
        {
            return [];
        }

        return await _dbContext.Set<MovimientoKardex>()
            .AsNoTracking()
            .OrderByDescending(movimiento => movimiento.Fecha)
            .ThenByDescending(movimiento => movimiento.CreatedAt)
            .Take(cantidad)
            .ToListAsync(cancellationToken);
    }

    public async Task AgregarInsumoAsync(Insumo insumo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(insumo);

        await _dbContext.Set<Insumo>().AddAsync(insumo, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Trackea el movimiento SIN guardar.
    ///
    /// No ejecuta <c>SaveChangesAsync</c> a proposito: un repositorio que confirma por
    /// su cuenta rompe la unidad de trabajo y deja al caso de uso sin forma de agrupar
    /// el movimiento con el cambio de stock en la misma transaccion. Quien confirma es
    /// <c>KardexService</c>, con un unico SaveChanges que cubre ambos.
    /// </summary>
    public async Task AgregarMovimientoAsync(
        MovimientoKardex movimiento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movimiento);

        await _dbContext.Set<MovimientoKardex>().AddAsync(movimiento, cancellationToken);
    }

    public void ActualizarInsumo(Insumo insumo)
    {
        ArgumentNullException.ThrowIfNull(insumo);

        _dbContext.Set<Insumo>().Update(insumo);
    }

    public async Task<decimal> CalcularStockAsync(Guid insumoId, CancellationToken cancellationToken = default)
    {
        // El saldo sale del historial, sumando el delta con signo de cada movimiento.
        // El Ajuste NO se ignora: su Cantidad ya es el delta (positiva si el conteo
        // dio mas, negativa si dio menos), asi que suma igual que los demas. Ignorarlo
        // hacia que el saldo se desfasara del StockActual en cada conteo fisico.
        var saldo = await _dbContext.Set<MovimientoKardex>()
            .AsNoTracking()
            .Where(movimiento => movimiento.InsumoId == insumoId)
            .Select(movimiento => new
            {
                movimiento.Tipo,
                movimiento.Cantidad
            })
            .ToListAsync(cancellationToken);

        return saldo.Sum(movimiento => movimiento.Tipo switch
        {
            TipoMovimientoKardex.Entrada => movimiento.Cantidad,
            TipoMovimientoKardex.Salida => -movimiento.Cantidad,
            TipoMovimientoKardex.Ajuste => movimiento.Cantidad,
            _ => 0m
        });
    }

    public async Task<EstadoAbastecimiento> CalcularEstadoAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default)
    {
        var insumo = await ObtenerInsumoAsync(insumoId, cancellationToken);

        return insumo is null
            ? EstadoAbastecimiento.Abastecido
            : _urgenciaCalculador.EvaluarEstado(
                insumo.StockActual,
                insumo.StockMinimo,
                insumo.ConsumoDiarioEstimado);
    }
}
