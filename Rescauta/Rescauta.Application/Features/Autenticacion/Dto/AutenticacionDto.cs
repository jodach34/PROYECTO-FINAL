namespace Rescauta.Application.Features.Autenticacion.Dto;

public static class AuthErrorCodes
{
    public const string Validacion = "auth.validacion";
    public const string CredencialesInvalidas = "auth.credenciales_invalidas";
    public const string CuentaPendiente = "auth.cuenta_pendiente";
    public const string CuentaRechazada = "auth.cuenta_rechazada";
    public const string CuentaBloqueada = "auth.cuenta_bloqueada";
    public const string CuentaInactiva = "auth.cuenta_inactiva";
    public const string CorreoRegistrado = "auth.correo_registrado";
    public const string TokenInvalido = "auth.token_invalido";
    public const string PasswordDebil = "auth.password_debil";
    public const string NoAutorizado = "auth.no_autorizado";
    public const string ErrorInterno = "auth.error_interno";
}

public sealed record RegistrarDonanteDto
{
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Organizacion { get; init; }
}

public sealed record SolicitarOperadorDto
{
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Organizacion { get; init; } = string.Empty;
    public Guid? ComedorSolicitadoId { get; init; }
    public string? Mensaje { get; init; }
}

public sealed record LoginDto
{
    public string Correo { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool Recordarme { get; init; }
}

public sealed record RecuperarPasswordDto
{
    public string Correo { get; init; } = string.Empty;
}

public sealed record RestablecerPasswordDto
{
    public string Token { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed record CambiarPasswordDto
{
    public string PasswordActual { get; init; } = string.Empty;
    public string PasswordNueva { get; init; } = string.Empty;
}

public sealed record ComedorAsignadoDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
}

public sealed record UsuarioSesionDto
{
    public Guid UsuarioId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Rol { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string? Organizacion { get; init; }
    public IReadOnlyList<ComedorAsignadoDto> Comedores { get; init; } = [];
}

public sealed record LoginResultDto
{
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiraAt { get; init; }
    public UsuarioSesionDto Usuario { get; init; } = new();
}

public sealed record RecuperacionResultDto
{
    public string Mensaje { get; init; } = string.Empty;
    public string? EnlaceDesarrollo { get; init; }
}
