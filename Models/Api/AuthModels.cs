namespace PROYECTO_FINAL.Models.Api;

public sealed class LoginRequest
{
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Recordarme { get; set; }
}

public sealed class RegistroDonanteRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Organizacion { get; set; }
}

public sealed class SolicitudOperadorRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Organizacion { get; set; } = string.Empty;
    public Guid? ComedorSolicitadoId { get; set; }
    public string? Mensaje { get; set; }
}

public sealed class RecuperarPasswordRequest
{
    public string Correo { get; set; } = string.Empty;
}

public sealed class RestablecerPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class CambiarPasswordRequest
{
    public string PasswordActual { get; set; } = string.Empty;
    public string PasswordNueva { get; set; } = string.Empty;
}

public sealed class ComedorAsignado
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public sealed class UsuarioSesion
{
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? Organizacion { get; set; }
    public List<ComedorAsignado> Comedores { get; set; } = [];
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiraAt { get; set; }
    public UsuarioSesion Usuario { get; set; } = new();
}

public sealed class RecuperacionResponse
{
    public string Mensaje { get; set; } = string.Empty;
    public string? EnlaceDesarrollo { get; set; }
}

public sealed class AdminUsuario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? Organizacion { get; set; }
    public DateTimeOffset FechaRegistro { get; set; }
    public DateTimeOffset? UltimoAcceso { get; set; }
    public List<ComedorAsignado> Comedores { get; set; } = [];
}

public sealed class SolicitudOperadorAdmin
{
    public Guid SolicitudId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Organizacion { get; set; } = string.Empty;
    public Guid? ComedorSolicitadoId { get; set; }
    public string? ComedorSolicitadoNombre { get; set; }
    public string? Mensaje { get; set; }
    public DateTimeOffset FechaSolicitud { get; set; }
}

public sealed class ComedorOpcion
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public sealed class AprobarOperadorRequest
{
    public List<Guid> ComedorIds { get; set; } = [];
}

public sealed class RechazarSolicitudRequest
{
    public string Motivo { get; set; } = string.Empty;
}

public sealed class CambiarRolRequest
{
    public string Rol { get; set; } = string.Empty;
}

public sealed class CambiarEstadoUsuarioRequest
{
    public string Estado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}

public sealed class ActualizarAsignacionesRequest
{
    public List<Guid> ComedorIds { get; set; } = [];
}
