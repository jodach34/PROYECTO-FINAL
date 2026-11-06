namespace Rescauta.Domain.Common;

/// <summary>
/// Raiz de toda entidad persistente del dominio.
/// Reglas: la clave primaria es Guid (se genera en el cliente, evita colisiones al fusionar
/// ramas de los distintos módulos) y el versionado de concurrencia es un rowversion.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>
    /// Token de concurrencia optimista. Lo mantiene EF Core; las entidades de solo lectura
    /// pueden living sin tocarlo.
    /// </summary>
    public byte[]? RowVersion { get; protected set; }

    public DateTimeOffset CreatedAt { get; internal set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; internal set; }

    public DateTimeOffset? UpdatedAt { get; internal set; }

    public string? UpdatedBy { get; internal set; }

    public bool IsDeleted { get; internal set; }
}
