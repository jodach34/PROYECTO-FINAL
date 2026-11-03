using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Usuarios;

public sealed class SolicitudOperador : BaseEntity
{
    public const int MensajeMaxLength = 1000;

    private SolicitudOperador()
    {
        Organizacion = string.Empty;
    }

    public static SolicitudOperador Crear(
        Guid usuarioId,
        string organizacion,
        Guid? comedorSolicitadoId = null,
        string? mensaje = null)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("El usuario es obligatorio.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(organizacion))
        {
            throw new DomainException("La organizacion o comedor es obligatorio.", nameof(organizacion));
        }

        if (mensaje?.Trim().Length > MensajeMaxLength)
        {
            throw new DomainException(
                $"El mensaje supera los {MensajeMaxLength} caracteres.",
                nameof(mensaje));
        }

        return new SolicitudOperador
        {
            UsuarioId = usuarioId,
            Organizacion = organizacion.Trim(),
            ComedorSolicitadoId = comedorSolicitadoId,
            Mensaje = string.IsNullOrWhiteSpace(mensaje) ? null : mensaje.Trim(),
            Estado = EstadoSolicitud.Pendiente
        };
    }

    public Guid UsuarioId { get; private set; }

    public string Organizacion { get; private set; }

    public Guid? ComedorSolicitadoId { get; private set; }

    public string? Mensaje { get; private set; }

    public EstadoSolicitud Estado { get; private set; }

    public Guid? RevisadoPor { get; private set; }

    public DateTimeOffset? FechaRevision { get; private set; }

    public string? MotivoRechazo { get; private set; }

    public void Aprobar(Guid administradorId, DateTimeOffset ahora)
    {
        if (Estado != EstadoSolicitud.Pendiente)
        {
            throw new DomainException("La solicitud ya fue revisada.");
        }

        Estado = EstadoSolicitud.Aprobada;
        RevisadoPor = administradorId;
        FechaRevision = ahora;
    }

    public void Rechazar(Guid administradorId, string motivo, DateTimeOffset ahora)
    {
        if (Estado != EstadoSolicitud.Pendiente)
        {
            throw new DomainException("La solicitud ya fue revisada.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("Indica el motivo del rechazo.", nameof(motivo));
        }

        Estado = EstadoSolicitud.Rechazada;
        RevisadoPor = administradorId;
        FechaRevision = ahora;
        MotivoRechazo = motivo.Trim();
    }
}
