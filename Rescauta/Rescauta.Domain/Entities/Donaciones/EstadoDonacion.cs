namespace Rescauta.Domain.Entities.Donaciones;

/// <summary>
/// Estados por los que puede pasar una donacion. El flujo normal es
/// Pendiente -> Confirmada -> EnTransito -> Entregada. Cancelada y Anulada son salidas
/// terminales y no permiten volver atras.
///
/// Se persiste como texto (ver DonacionConfig) y no como numero: el historial de una
/// organizacion de rescate se audita a mano y "Entregada" se lee mucho mejor que "3".
/// </summary>
public enum EstadoDonacion
{
    /// <summary>Registrada por un donante, todavia sin verificar.</summary>
    Pendiente = 0,

    /// <summary>La recepcion confirmo que recibio fisicamente los insumos.</summary>
    Confirmada = 1,

    /// <summary>Confirmada y en camino al punto de rescate.</summary>
    EnTransito = 2,

    /// <summary>Entregada al punto de rescate. Estado final.</summary>
    Entregada = 3,

    /// <summary>El donante o la recepcion la cancelo antes de entregarla. Estado final.</summary>
    Cancelada = 4,

    /// <summary>Se rechazo por no cumplir los criterios. Estado final.</summary>
    Anulada = 5
}
