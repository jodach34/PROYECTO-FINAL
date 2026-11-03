using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Usuarios;

public sealed class TokenRecuperacion : BaseEntity
{
    public const int TokenHashMaxLength = 64;

    private TokenRecuperacion()
    {
        TokenHash = string.Empty;
    }

    public static TokenRecuperacion Crear(
        Guid usuarioId,
        string tokenHash,
        DateTimeOffset expiraAt)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("El usuario es obligatorio.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length > TokenHashMaxLength)
        {
            throw new DomainException("El token de recuperacion no es valido.", nameof(tokenHash));
        }

        return new TokenRecuperacion
        {
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            ExpiraAt = expiraAt
        };
    }

    public Guid UsuarioId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiraAt { get; private set; }

    public DateTimeOffset? UsadoAt { get; private set; }

    public Usuario Usuario { get; private set; } = null!;

    public bool EsValido(DateTimeOffset ahora) => UsadoAt is null && ExpiraAt > ahora;

    public void MarcarUsado(DateTimeOffset ahora)
    {
        UsadoAt ??= ahora;
    }
}
