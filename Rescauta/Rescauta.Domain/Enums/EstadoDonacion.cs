namespace Rescauta.Domain.Enums;

/// <summary>
/// Ciclo de vida de una donacion. Las transiciones validas las controla la entidad
/// <c>Donacion</c>; este enum solo describe los estados posibles.
/// </summary>
public enum EstadoDonacion
{
    /// <summary>Registrada por el donante, todavia sin clasificar.</summary>
    Recibida = 1,

    /// <summary>Se reviso el insumo y se decidio a que comedor se asigna.</summary>
    Clasificada = 2,

    /// <summary>Asignada a un comedor concreto, pendiente de entrega.</summary>
    Asignada = 3,

    /// <summary>Entregada en el punto de recojo. Estado terminal.</summary>
    Entregada = 4
}
