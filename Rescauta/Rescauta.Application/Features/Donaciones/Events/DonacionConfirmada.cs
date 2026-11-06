using MediatR;

namespace Rescauta.Application.Features.Donaciones.Events;

/// <summary>
/// Evento de dominio en proceso: la donacion acaba de pasar a Confirmada y corresponde
/// asentar su entrada en el kardex.
///
/// Vive en Application y no en Domain porque implementa <see cref="INotification"/> de
/// MediatR, y Domain no referencia paquetes de infraestructura. La regla de negocio sigue
/// siendo del dominio (Donacion.ConfirmarRecepcion valida la transicion); este evento solo
/// comunica el hecho a otro modulo.
/// </summary>
public sealed record DonacionConfirmada(
    Guid DonacionId,
    string CodigoSeguimiento,
    Guid InsumoId,
    Guid ComedorId,
    string InsumoNombre,
    string UnidadMedida,
    string ComedorNombre,
    int Cantidad,
    string Donante,
    string? PuntoRecojo,
    DateTimeOffset FechaConfirmacion) : INotification;
