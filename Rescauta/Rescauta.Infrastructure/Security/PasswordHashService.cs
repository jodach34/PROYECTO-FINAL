using Microsoft.AspNetCore.Identity;
using Rescauta.Application.Features.Autenticacion;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Security;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<Usuario> _hasher = new();
    private readonly Usuario _sujeto = Usuario.CrearDonante(
        "password-hasher",
        "password-hasher@rescauta.local",
        "placeholder");

    public string Hash(string password) => _hasher.HashPassword(_sujeto, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(_sujeto, hash, password) != PasswordVerificationResult.Failed;
}
