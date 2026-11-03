using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Features.Autenticacion;
using Rescauta.Domain.Entities.Usuarios;

namespace Rescauta.Infrastructure.Persistence.Seed;

public static class UsuarioSemilla
{
    public static async Task SembrarAsync(
        AppDbContext context,
        IPasswordHashService passwordHashService,
        AuthOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SeedAdminEmail) ||
            string.IsNullOrWhiteSpace(options.SeedAdminPassword))
        {
            return;
        }

        var correo = Usuario.NormalizarCorreo(options.SeedAdminEmail);

        if (await context.Set<Usuario>().AnyAsync(u => u.Correo == correo, cancellationToken))
        {
            logger.LogInformation("La semilla de administrador se omite: la cuenta ya existe.");
            return;
        }

        var usuario = Usuario.CrearAdministrador(
            "Administrador Rescauta",
            correo,
            passwordHashService.Hash(options.SeedAdminPassword));

        context.Set<Usuario>().Add(usuario);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cuenta administradora inicial creada para {Correo}.", correo);
    }
}
