using MediatR;

namespace Rescauta.Application.Features.Donaciones.Events;

/// <summary>
/// Evento de dominio en proceso: una donacion que ya habia ingresado stock se cancelo o
/// anulo, y el inventario debe revertir la entrada.
/// </summary>
public sealed record DonacionStockRevertido(
    Guid DonacionId,
    string CodigoSeguimiento,
    Guid InsumoId,
    Guid ComedorId,
    string InsumoNombre,
    string UnidadMedida,
    int Cantidad,
    string Motivo,
    DateTimeOffset FechaCierre) : INotification;
