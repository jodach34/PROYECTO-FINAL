using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Usuarios;

public sealed class AsignacionComedor : BaseEntity
{
    private AsignacionComedor()
    {
    }

    public static AsignacionComedor Crear(Guid usuarioId, Guid comedorId)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("El usuario es obligatorio.", nameof(usuarioId));
        }

        if (comedorId == Guid.Empty)
        {
            throw new DomainException("El comedor es obligatorio.", nameof(comedorId));
        }

        return new AsignacionComedor
        {
            UsuarioId = usuarioId,
            ComedorId = comedorId,
            Activa = true
        };
    }

    public Guid UsuarioId { get; private set; }

    public Guid ComedorId { get; private set; }

    public bool Activa { get; private set; }

    public void Desactivar() => Activa = false;

    public void Reactivar() => Activa = true;
}
