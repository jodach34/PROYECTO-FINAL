using System.ComponentModel.DataAnnotations;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu contraseña.")]
    public string Password { get; set; } = string.Empty;

    public bool Recordarme { get; set; }

    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}

public sealed class RegistroViewModel
{
    [Required(ErrorMessage = "Escribe tu nombre.")]
    [StringLength(160)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    public string Correo { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Organizacion { get; set; }

    public string Rol { get; set; } = "donante";

    [Required(ErrorMessage = "Escribe una contraseña.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repite la contraseña.")]
    public string PasswordConfirmar { get; set; } = string.Empty;

    public bool AceptaTerminos { get; set; }

    public string? Error { get; set; }

    public string? Mensaje { get; set; }
}

public sealed class RecuperarPasswordViewModel
{
    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    public string Correo { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? Mensaje { get; set; }

    public string? EnlaceDesarrollo { get; set; }
}

public sealed class RestablecerPasswordViewModel
{
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe una contraseña nueva.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repite la contraseña.")]
    public string PasswordConfirmar { get; set; } = string.Empty;

    public string? Error { get; set; }
}

public sealed class MiCuentaViewModel
{
    public UsuarioSesion? Usuario { get; init; }

    public string? Error { get; init; }

    public string? Exito { get; init; }
}

public sealed class CambiarPasswordViewModel
{
    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    public string PasswordActual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe la contraseña nueva.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string PasswordNueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repite la contraseña nueva.")]
    public string PasswordConfirmar { get; set; } = string.Empty;

    public string? Error { get; set; }
}

public sealed class AdminUsuariosViewModel
{
    public IReadOnlyList<AdminUsuario> Usuarios { get; init; } = [];

    public IReadOnlyList<SolicitudOperadorAdmin> Solicitudes { get; init; } = [];

    public IReadOnlyList<ComedorOpcion> Comedores { get; init; } = [];

    public string? Busqueda { get; init; }

    public string? Error { get; init; }

    public string? Exito { get; init; }
}
