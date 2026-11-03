using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Usuarios;

public sealed class Usuario : BaseEntity, IAggregateRoot
{
    public const int NombreMaxLength = 160;
    public const int CorreoMaxLength = 254;
    public const int OrganizacionMaxLength = 200;
    public const int PasswordHashMaxLength = 512;

    private Usuario()
    {
        Nombre = string.Empty;
        Correo = string.Empty;
        PasswordHash = string.Empty;
    }

    public static Usuario CrearDonante(
        string nombre,
        string correo,
        string passwordHash,
        string? organizacion = null)
    {
        return Crear(nombre, correo, passwordHash, RolUsuario.Donante, EstadoUsuario.Activo, organizacion);
    }

    public static Usuario CrearOperadorPendiente(
        string nombre,
        string correo,
        string passwordHash,
        string? organizacion = null)
    {
        return Crear(nombre, correo, passwordHash, RolUsuario.Operador, EstadoUsuario.Pendiente, organizacion);
    }

    public static Usuario CrearAdministrador(
        string nombre,
        string correo,
        string passwordHash)
    {
        return Crear(nombre, correo, passwordHash, RolUsuario.Administrador, EstadoUsuario.Activo, null);
    }

    public static Usuario Crear(
        string nombre,
        string correo,
        string passwordHash,
        RolUsuario rol,
        EstadoUsuario estado,
        string? organizacion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre es obligatorio.", nameof(nombre));
        }

        if (nombre.Trim().Length > NombreMaxLength)
        {
            throw new DomainException(
                $"El nombre supera los {NombreMaxLength} caracteres.",
                nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(correo))
        {
            throw new DomainException("El correo es obligatorio.", nameof(correo));
        }

        var correoNormalizado = NormalizarCorreo(correo);

        if (correoNormalizado.Length > CorreoMaxLength)
        {
            throw new DomainException(
                $"El correo supera los {CorreoMaxLength} caracteres.",
                nameof(correo));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("La contrasena es obligatoria.", nameof(passwordHash));
        }

        if (passwordHash.Length > PasswordHashMaxLength)
        {
            throw new DomainException("El hash de contrasena no es valido.", nameof(passwordHash));
        }

        if (organizacion?.Trim().Length > OrganizacionMaxLength)
        {
            throw new DomainException(
                $"La organizacion supera los {OrganizacionMaxLength} caracteres.",
                nameof(organizacion));
        }

        if (rol == RolUsuario.Visitante)
        {
            throw new DomainException("El rol Visitante no se asigna a una cuenta.", nameof(rol));
        }

        return new Usuario
        {
            Nombre = nombre.Trim(),
            Correo = correoNormalizado,
            PasswordHash = passwordHash,
            Rol = rol,
            Estado = estado,
            Organizacion = string.IsNullOrWhiteSpace(organizacion) ? null : organizacion.Trim()
        };
    }

    public string Nombre { get; private set; }

    public string Correo { get; private set; }

    public string PasswordHash { get; private set; }

    public RolUsuario Rol { get; private set; }

    public EstadoUsuario Estado { get; private set; }

    public string? Organizacion { get; private set; }

    public DateTimeOffset? UltimoAcceso { get; private set; }

    public int IntentosFallidos { get; private set; }

    public DateTimeOffset? BloqueadoHasta { get; private set; }

    public string? MotivoRechazo { get; private set; }

    public Guid? AprobadoPor { get; private set; }

    public DateTimeOffset? FechaAprobacion { get; private set; }

    public bool EstaActivo => Estado == EstadoUsuario.Activo;

    public bool EstaPendiente => Estado == EstadoUsuario.Pendiente;

    public bool EstaBloqueado(DateTimeOffset ahora) =>
        Estado == EstadoUsuario.Bloqueado || (BloqueadoHasta is { } hasta && hasta > ahora);

    public void EstablecerPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("El hash de contrasena es obligatorio.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public void RegistrarAcceso(DateTimeOffset ahora)
    {
        if (Estado != EstadoUsuario.Activo)
        {
            throw new DomainException("La cuenta no esta activa.");
        }

        UltimoAcceso = ahora;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public void RegistrarIntentoFallido(DateTimeOffset ahora, int maximoIntentos, TimeSpan bloqueo)
    {
        IntentosFallidos++;

        if (IntentosFallidos >= maximoIntentos)
        {
            BloqueadoHasta = ahora.Add(bloqueo);
            IntentosFallidos = 0;
        }
    }

    public void Aprobar(Guid administradorId, DateTimeOffset ahora)
    {
        if (Estado != EstadoUsuario.Pendiente)
        {
            throw new DomainException("Solo se puede aprobar una cuenta pendiente.");
        }

        if (Rol is not (RolUsuario.Operador or RolUsuario.Coordinador))
        {
            throw new DomainException("Solo se pueden aprobar cuentas operativas.");
        }

        Estado = EstadoUsuario.Activo;
        AprobadoPor = administradorId;
        FechaAprobacion = ahora;
        MotivoRechazo = null;
        BloqueadoHasta = null;
    }

    public void Rechazar(Guid administradorId, string motivo)
    {
        if (Estado != EstadoUsuario.Pendiente)
        {
            throw new DomainException("Solo se puede rechazar una cuenta pendiente.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("Indica el motivo del rechazo.", nameof(motivo));
        }

        Estado = EstadoUsuario.Rechazado;
        AprobadoPor = administradorId;
        FechaAprobacion = DateTimeOffset.UtcNow;
        MotivoRechazo = motivo.Trim();
    }

    public void CambiarRol(RolUsuario rol)
    {
        if (rol == RolUsuario.Visitante)
        {
            throw new DomainException("El rol Visitante no se asigna a una cuenta.", nameof(rol));
        }

        Rol = rol;

        if (rol == RolUsuario.Administrador)
        {
            Estado = EstadoUsuario.Activo;
        }
    }

    public void Activar()
    {
        Estado = EstadoUsuario.Activo;
        MotivoRechazo = null;
        BloqueadoHasta = null;
    }

    public void Bloquear()
    {
        Estado = EstadoUsuario.Bloqueado;
        BloqueadoHasta = null;
    }

    public void Desbloquear()
    {
        if (Estado == EstadoUsuario.Bloqueado)
        {
            Estado = EstadoUsuario.Activo;
        }

        BloqueadoHasta = null;
        IntentosFallidos = 0;
    }

    public void ActualizarPerfil(string nombre, string? organizacion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre es obligatorio.", nameof(nombre));
        }

        Nombre = nombre.Trim();
        Organizacion = string.IsNullOrWhiteSpace(organizacion) ? null : organizacion.Trim();
    }

    public static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();
}
