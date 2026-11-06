using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Kardex;
using Rescauta.Application.Features.Reportes.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Donaciones;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Application.Features.Reportes;

/// <summary>
/// Implementacion de los reportes basicos.
///
/// Las sumas decimales se hacen en memoria y no en SQL porque SQLite no admite SUM sobre
/// decimal, que es el tipo de Cantidad y StockActual. Se traen solo las columnas necesarias.
/// </summary>
public sealed class ReporteService(IAppDbContext dbContext) : IReporteService
{
    public async Task<Result<ReporteDonacionesRecibidasDto>> ObtenerDonacionesRecibidasAsync(
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var consulta = dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .Where(d => !d.IsDeleted &&
                        (d.Estado == EstadoDonacion.Confirmada ||
                         d.Estado == EstadoDonacion.EnTransito ||
                         d.Estado == EstadoDonacion.Entregada));

        if (!usuario.EsAdministrador)
        {
            var comedores = usuario.ComedoresAsignados.ToArray();

            if (comedores.Length == 0)
            {
                return Result<ReporteDonacionesRecibidasDto>.Success(
                    new ReporteDonacionesRecibidasDto());
            }

            consulta = consulta.Where(d => comedores.Contains(d.ComedorId));
        }

        var cantidades = await consulta
            .Select(d => d.Cantidad)
            .ToListAsync(cancellationToken);

        return Result<ReporteDonacionesRecibidasDto>.Success(
            new ReporteDonacionesRecibidasDto
            {
                TotalDonaciones = cantidades.Count,
                TotalCantidad = cantidades.Sum()
            });
    }

    public async Task<Result<IReadOnlyList<ReporteStockInsumoDto>>> ObtenerStockDisponibleAsync(
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var consulta = dbContext
            .Set<Insumo>()
            .AsNoTracking()
            .Where(i => !i.IsDeleted);

        if (!usuario.EsAdministrador)
        {
            var comedores = usuario.ComedoresAsignados.ToArray();

            if (comedores.Length == 0)
            {
                return Result<IReadOnlyList<ReporteStockInsumoDto>>.Success([]);
            }

            consulta = consulta.Where(i => comedores.Contains(i.ComedorId));
        }

        var filas = await consulta
            .Select(i => new { i.Nombre, i.UnidadMedida, i.StockActual })
            .ToListAsync(cancellationToken);

        var stock = filas
            .GroupBy(i => new { i.Nombre, i.UnidadMedida })
            .Select(grupo => new ReporteStockInsumoDto
            {
                Insumo = grupo.Key.Nombre,
                UnidadMedida = grupo.Key.UnidadMedida,
                StockDisponible = grupo.Sum(i => i.StockActual)
            })
            .OrderBy(item => item.Insumo, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.UnidadMedida, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<ReporteStockInsumoDto>>.Success(stock);
    }
}
