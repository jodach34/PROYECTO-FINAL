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
        bool requireAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var tieneCorreo = !string.IsNullOrWhiteSpace(options.SeedAdminEmail);
        var tienePassword = !string.IsNullOrWhiteSpace(options.SeedAdminPassword);

        if (tieneCorreo != tienePassword)
        {
            if (requireAdmin)
            {
                throw new InvalidOperationException(
                    "Auth:SeedAdminEmail y Auth:SeedAdminPassword deben configurarse juntos en produccion.");
            }

            logger.LogWarning(
                "La semilla de administrador se omite: Auth:SeedAdminEmail y Auth:SeedAdminPassword deben configurarse juntos.");
            return;
        }

        if (!tieneCorreo)
        {
            if (requireAdmin)
            {
                if (!await ExisteAdministradorAsync(context, cancellationToken))
                {
                    throw new InvalidOperationException(
                        "No existe una cuenta administradora y no se configuro " +
                        "Auth:SeedAdminEmail/Auth:SeedAdminPassword. Configura ambos valores en Render " +
                        "antes de arrancar la API.");
                }

                logger.LogInformation(
                    "La semilla de administrador se omite: ya existe una cuenta administradora.");
            }

            return;
        }

        var correo = Usuario.NormalizarCorreo(options.SeedAdminEmail!);

        if (!correo.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Auth:SeedAdminEmail no es un correo valido.");
        }

        var existente = await context
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Correo == correo, cancellationToken);

        if (existente is not null)
        {
            if (existente.Rol != RolUsuario.Administrador)
            {
                logger.LogWarning(
                    "La cuenta configurada en Auth:SeedAdminEmail ya existe con otro rol; no se modifica.");
            }
            else
            {
                logger.LogInformation(
                    "La semilla de administrador se omite: la cuenta configurada ya existe.");
            }

            if (requireAdmin && !await ExisteAdministradorAsync(context, cancellationToken))
            {
                throw new InvalidOperationException(
                    "No existe una cuenta administradora. La cuenta configurada en Auth:SeedAdminEmail " +
                    "ya existe con otro rol y no se modifica automaticamente. Configura un correo de " +
                    "administrador distinto.");
            }

            return;
        }

        var errorPassword = PasswordPolicy.Validar(
            options.SeedAdminPassword,
            options.PasswordMinLength);

        if (errorPassword is not null)
        {
            throw new InvalidOperationException(
                $"Auth:SeedAdminPassword no cumple la politica de contrasenas: {errorPassword}");
        }

        var usuario = Usuario.CrearAdministrador(
            "Administrador Rescauta",
            correo,
            passwordHashService.Hash(options.SeedAdminPassword!));

        context.Set<Usuario>().Add(usuario);
        await context.SaveChangesAsync(cancellationToken);

        if (requireAdmin && !await ExisteAdministradorAsync(context, cancellationToken))
        {
            throw new InvalidOperationException(
                "No se pudo crear la cuenta administradora inicial. Revisa " +
                "Auth:SeedAdminEmail y Auth:SeedAdminPassword.");
        }

        logger.LogInformation("Cuenta administradora inicial creada.");
    }

    private static Task<bool> ExisteAdministradorAsync(
        AppDbContext context,
        CancellationToken cancellationToken) =>
        context
            .Set<Usuario>()
            .AnyAsync(u => u.Rol == RolUsuario.Administrador, cancellationToken);
}
