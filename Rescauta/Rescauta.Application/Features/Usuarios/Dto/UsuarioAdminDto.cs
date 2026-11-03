namespace Rescauta.Application.Features.Usuarios.Dto;

public static class UsuarioAdminErrorCodes
{
    public const string Validacion = "usuarios.validacion";
    public const string NoEncontrado = "usuarios.no_encontrado";
    public const string SolicitudNoEncontrada = "usuarios.solicitud_no_encontrada";
    public const string UltimoAdministrador = "usuarios.ultimo_administrador";
    public const string AsignacionInvalida = "usuarios.asignacion_invalida";
}

public sealed record ComedorOpcionDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
}

public sealed record UsuarioAdminDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Rol { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string? Organizacion { get; init; }
    public DateTimeOffset FechaRegistro { get; init; }
    public DateTimeOffset? UltimoAcceso { get; init; }
    public IReadOnlyList<ComedorOpcionDto> Comedores { get; init; } = [];
}

public sealed record SolicitudOperadorAdminDto
{
    public Guid SolicitudId { get; init; }
    public Guid UsuarioId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Organizacion { get; init; } = string.Empty;
    public Guid? ComedorSolicitadoId { get; init; }
    public string? ComedorSolicitadoNombre { get; init; }
    public string? Mensaje { get; init; }
    public DateTimeOffset FechaSolicitud { get; init; }
}

public sealed record AprobarOperadorDto
{
    public IReadOnlyList<Guid> ComedorIds { get; init; } = [];
}

public sealed record RechazarSolicitudDto
{
    public string Motivo { get; init; } = string.Empty;
}

public sealed record CambiarRolDto
{
    public string Rol { get; init; } = string.Empty;
}

public sealed record CambiarEstadoUsuarioDto
{
    public string Estado { get; init; } = string.Empty;
    public string? Motivo { get; init; }
}

public sealed record ActualizarAsignacionesDto
{
    public IReadOnlyList<Guid> ComedorIds { get; init; } = [];
}
