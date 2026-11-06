using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Donaciones.Events;

/// <summary>
/// Asienta la entrada al kardex cuando la donacion se confirma. Se ejecuta en la misma
/// unidad de trabajo que el caso de uso, antes del SaveChanges: si el insumo no existe o
/// no coincide con el comedor de la donacion, la transicion completa se rechaza y no se
/// guarda ningun estado a medias.
/// </summary>
public sealed class DonacionConfirmadaHandler(
    IAppDbContext dbContext,
    InventarioDonacionResultado resultado,
    ILogger<DonacionConfirmadaHandler> logger) : INotificationHandler<DonacionConfirmada>
{
    public async Task Handle(DonacionConfirmada notification, CancellationToken cancellationToken)
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
                $"El insumo {notification.InsumoNombre} no pertenece al comedor " +
                $"{notification.ComedorNombre}.");
        }

        var stockPrevio = insumo.StockActual;
        var concepto = string.IsNullOrWhiteSpace(notification.PuntoRecojo)
            ? $"Donacion {notification.CodigoSeguimiento} de {notification.Donante}"
            : $"Donacion {notification.CodigoSeguimiento} de {notification.Donante} - " +
              notification.PuntoRecojo;

        var asiento = insumo.RegistrarEntrada(
            notification.Cantidad,
            notification.Donante,
            concepto,
            notification.FechaConfirmacion);

        // Igual que en KardexService: la coleccion del agregado es de solo lectura, asi que
        // EF no descubre el asiento nuevo por navegacion. Hay que registrarlo explicitamente.
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
            "Entrada de kardex preparada por donacion confirmada. Donacion={Codigo}, " +
            "Insumo={InsumoId}, Cantidad={Cantidad}, StockPrevio={StockPrevio}, StockActual={StockActual}",
            notification.CodigoSeguimiento,
            notification.InsumoId,
            notification.Cantidad,
            stockPrevio,
            insumo.StockActual);
    }
}
