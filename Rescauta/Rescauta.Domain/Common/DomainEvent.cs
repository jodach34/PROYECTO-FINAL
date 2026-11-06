namespace Rescauta.Domain.Common;

/// <summary>
/// Placeholder para eventos de dominio. Cada modulo (Mapas, Kardex, Donaciones) puede
/// definir los suyos heredando de esta clase. Ningun evento debe transportar entidades
/// completas entre capas: solo ids y datos primitivos.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
