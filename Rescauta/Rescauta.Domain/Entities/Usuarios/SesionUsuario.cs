using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Usuarios;

public sealed class SesionUsuario : BaseEntity
{
    public const int TokenHashMaxLength = 64;
    public const int IpMaxLength = 64;
    public const int UserAgentMaxLength = 512;

    private SesionUsuario()
    {
        TokenHash = string.Empty;
    }

    public static SesionUsuario Crear(
        Guid usuarioId,
        string tokenHash,
        DateTimeOffset expiraAt,
        string? ip,
        string? userAgent)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new DomainException("El usuario es obligatorio.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length > TokenHashMaxLength)
        {
            throw new DomainException("El token de sesion no es valido.", nameof(tokenHash));
        }

        return new SesionUsuario
        {
            UsuarioId = usuarioId,
            TokenHash = tokenHash,
            ExpiraAt = expiraAt,
            Ip = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent)
                ? null
                : userAgent.Trim()[..Math.Min(userAgent.Trim().Length, UserAgentMaxLength)]
        };
    }

    public Guid UsuarioId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiraAt { get; private set; }

    public DateTimeOffset? RevocadaAt { get; private set; }

    public DateTimeOffset? UltimoUsoAt { get; private set; }

    public string? Ip { get; private set; }

    public string? UserAgent { get; private set; }

    public Usuario Usuario { get; private set; } = null!;

    public bool EstaActiva(DateTimeOffset ahora) =>
        RevocadaAt is null && ExpiraAt > ahora && !IsDeleted;

    public void Revocar(DateTimeOffset ahora)
    {
        RevocadaAt ??= ahora;
    }

    public void MarcarUso(DateTimeOffset ahora)
    {
        UltimoUsoAt = ahora;
    }
}
