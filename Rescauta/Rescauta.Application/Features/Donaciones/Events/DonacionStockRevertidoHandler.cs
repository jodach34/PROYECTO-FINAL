using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Donaciones.Events;

/// <summary>
/// Revierte el stock cuando una donacion confirmada se cancela o anula. La salida usa el
/// metodo de negocio del agregado, que rechaza la operacion si el insumo ya no tiene saldo
/// suficiente (por ejemplo, porque parte de esa donacion ya se consumio).
/// </summary>
public sealed class DonacionStockRevertidoHandler(
    IAppDbContext dbContext,
    InventarioDonacionResultado resultado,
    ILogger<DonacionStockRevertidoHandler> logger) : INotificationHandler<DonacionStockRevertido>
{
    public async Task Handle(DonacionStockRevertido notification, CancellationToken cancellationToken)
    {
        var insumo = await dbContext
            .Set<Insumo>()
            .FirstOrDefaultAsync(x => x.Id == notification.InsumoId, cancellationToken);

        if (insumo is null)
        {
            throw new DomainException(
                $"No existe el insumo {notification.InsumoId} de la donacion " +
                $"{notification.CodigoSeguimiento}.");
        }

        if (insumo.ComedorId != notification.ComedorId)
        {
            throw new DomainException(
                $"El insumo {notification.InsumoNombre} no pertenece al comedor de la donacion.");
        }

        var stockPrevio = insumo.StockActual;
        var concepto =
            $"Reversion de donacion {notification.CodigoSeguimiento}: {notification.Motivo}";

        var asiento = insumo.RegistrarSalida(
            notification.Cantidad,
            Insumo.ResponsableSistema,
            concepto,
            notification.FechaCierre);

        dbContext.Set<MovimientoKardex>().Add(asiento);

        resultado.InsumoId = insumo.Id;
        resultado.MovimientoId = asiento.Id;
        resultado.StockPrevio = stockPrevio;
        resultado.StockActual = insumo.StockActual;
        resultado.InsumoNombre = insumo.Nombre;
        resultado.UnidadMedida = insumo.UnidadMedida;
        resultado.TipoMovimiento = asiento.TipoMovimiento;
        resultado.Cantidad = asiento.Cantidad;
        resultado.FechaHora = asiento.FechaHora;

        logger.LogInformation(
            "Salida de kardex preparada por reversion de donacion. Donacion={Codigo}, " +
            "Insumo={InsumoId}, Cantidad={Cantidad}, StockPrevio={StockPrevio}, StockActual={StockActual}",
            notification.CodigoSeguimiento,
            notification.InsumoId,
            notification.Cantidad,
            stockPrevio,
            insumo.StockActual);
    }
}
