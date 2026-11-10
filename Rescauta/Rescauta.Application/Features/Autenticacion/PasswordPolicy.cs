namespace Rescauta.Application.Features.Autenticacion;

public static class PasswordPolicy
{
    public static string? Validar(string? password, int minLength)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < minLength)
        {
            return $"La contrasena debe tener al menos {minLength} caracteres.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "La contrasena debe incluir al menos una letra y un numero.";
        }

        return null;
    }
}
