using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Donaciones;

/// <summary>
/// Registro append-only de cada cambio de estado de una donacion. Se crea desde Application
/// despues de que el agregado valido la transicion, y sirve para auditar quien hizo el cambio,
/// cuando y con que motivo.
/// </summary>
public sealed class DonacionHistorial : BaseEntity
{
    /// <summary>Longitud maxima del motivo.</summary>
    public const int MotivoMaxLength = Donacion.MotivoCierreMaxLength;

    private DonacionHistorial()
    {
    }

    public static DonacionHistorial Crear(
        Guid donacionId,
        EstadoDonacion estadoAnterior,
        EstadoDonacion estadoNuevo,
        string? motivo = null,
        Guid? usuarioId = null,
        DateTimeOffset? fecha = null)
    {
        if (donacionId == Guid.Empty)
        {
            throw new DomainException("La donacion del historial es obligatoria.");
        }

        if (estadoAnterior == estadoNuevo)
        {
            throw new DomainException("El historial debe registrar un cambio de estado real.");
        }

        var motivoNormalizado = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();

        if (motivoNormalizado is not null && motivoNormalizado.Length > MotivoMaxLength)
        {
            throw new DomainException(
                $"El motivo del historial supera los {MotivoMaxLength} caracteres.",
                nameof(motivo));
        }

        return new DonacionHistorial
        {
            DonacionId = donacionId,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo,
            Motivo = motivoNormalizado,
            UsuarioId = usuarioId,
            Fecha = fecha ?? DateTimeOffset.UtcNow
        };
    }

    public Guid DonacionId { get; private set; }

    public EstadoDonacion EstadoAnterior { get; private set; }

    public EstadoDonacion EstadoNuevo { get; private set; }

    public string? Motivo { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset Fecha { get; private set; }
}
