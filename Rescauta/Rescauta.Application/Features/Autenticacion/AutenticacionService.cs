using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Autenticacion.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Usuarios;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Autenticacion;

public sealed class AutenticacionService(
    IAppDbContext dbContext,
    IPasswordHashService passwordHashService,
    IEmailSender emailSender,
    IOptions<AuthOptions> options,
    ILogger<AutenticacionService> logger) : IAutenticacionService
{
    private readonly AuthOptions _options = options.Value;

    public async Task<Result<UsuarioSesionDto>> RegistrarDonanteAsync(
        RegistrarDonanteDto registro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var error = ValidarRegistro(registro.Nombre, registro.Correo, registro.Password);
        if (error is not null)
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.Validacion, error);
        }

        var correo = Usuario.NormalizarCorreo(registro.Correo);

        if (await CorreoExisteAsync(correo, cancellationToken))
        {
            return Result<UsuarioSesionDto>.Failure(
                AuthErrorCodes.CorreoRegistrado,
                "Ya existe una cuenta con ese correo.");
        }

        try
        {
            var usuario = Usuario.CrearDonante(
                registro.Nombre,
                correo,
                passwordHashService.Hash(registro.Password),
                registro.Organizacion);

            dbContext.Set<Usuario>().Add(usuario);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<UsuarioSesionDto>.Success(await ConstruirUsuarioSesionAsync(usuario, cancellationToken));
        }
        catch (DomainException ex)
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.Validacion, ex.Message);
        }
        catch (DbUpdateException)
        {
            return Result<UsuarioSesionDto>.Failure(
                AuthErrorCodes.CorreoRegistrado,
                "Ya existe una cuenta con ese correo.");
        }
    }

    public async Task<Result<UsuarioSesionDto>> SolicitarOperadorAsync(
        SolicitarOperadorDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var error = ValidarRegistro(solicitud.Nombre, solicitud.Correo, solicitud.Password);
        if (error is not null)
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.Validacion, error);
        }

        if (string.IsNullOrWhiteSpace(solicitud.Organizacion))
        {
            return Result<UsuarioSesionDto>.Failure(
                AuthErrorCodes.Validacion,
                "Indica la organizacion o comedor al que representas.");
        }

        var correo = Usuario.NormalizarCorreo(solicitud.Correo);

        if (await CorreoExisteAsync(correo, cancellationToken))
        {
            return Result<UsuarioSesionDto>.Failure(
                AuthErrorCodes.CorreoRegistrado,
                "Ya existe una cuenta con ese correo.");
        }

        try
        {
            var usuario = Usuario.CrearOperadorPendiente(
                solicitud.Nombre,
                correo,
                passwordHashService.Hash(solicitud.Password),
                solicitud.Organizacion);

            var solicitudOperador = SolicitudOperador.Crear(
                usuario.Id,
                solicitud.Organizacion,
                solicitud.ComedorSolicitadoId,
                solicitud.Mensaje);

            dbContext.Set<Usuario>().Add(usuario);
            dbContext.Set<SolicitudOperador>().Add(solicitudOperador);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<UsuarioSesionDto>.Success(await ConstruirUsuarioSesionAsync(usuario, cancellationToken));
        }
        catch (DomainException ex)
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.Validacion, ex.Message);
        }
        catch (DbUpdateException)
        {
            return Result<UsuarioSesionDto>.Failure(
                AuthErrorCodes.CorreoRegistrado,
                "Ya existe una cuenta con ese correo.");
        }
    }

    public async Task<Result<LoginResultDto>> LoginAsync(
        LoginDto login,
        string? ip,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(login);

        if (string.IsNullOrWhiteSpace(login.Correo) || string.IsNullOrWhiteSpace(login.Password))
        {
            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.Validacion,
                "Escribe tu correo y contrasena.");
        }

        var correo = Usuario.NormalizarCorreo(login.Correo);
        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Correo == correo, cancellationToken);

        if (usuario is null)
        {
            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.CredencialesInvalidas,
                "El correo o la contrasena no son correctos.");
        }

        var ahora = DateTimeOffset.UtcNow;

        if (usuario.EstaBloqueado(ahora))
        {
            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.CuentaBloqueada,
                "La cuenta esta bloqueada temporalmente. Intenta de nuevo mas tarde.");
        }

        if (!passwordHashService.Verify(usuario.PasswordHash, login.Password))
        {
            usuario.RegistrarIntentoFallido(
                ahora,
                _options.MaxFailedAttempts,
                TimeSpan.FromMinutes(_options.LockoutMinutes));

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.CredencialesInvalidas,
                "El correo o la contrasena no son correctos.");
        }

        if (usuario.Estado == EstadoUsuario.Pendiente)
        {
            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.CuentaPendiente,
                "Tu solicitud esta pendiente de aprobacion.");
        }

        if (usuario.Estado == EstadoUsuario.Rechazado)
        {
            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.CuentaRechazada,
                "Tu solicitud fue rechazada. Contacta a la coordinacion.");
        }

        if (usuario.Estado != EstadoUsuario.Activo)
        {
            return Result<LoginResultDto>.Failure(
                AuthErrorCodes.CuentaInactiva,
                "La cuenta no esta habilitada.");
        }

        var duracion = login.Recordarme
            ? TimeSpan.FromDays(_options.RememberMeDays)
            : TimeSpan.FromHours(_options.SessionHours);

        var expiraAt = ahora.Add(duracion);
        var token = GenerarToken();
        var sesion = SesionUsuario.Crear(usuario.Id, HashToken(token), expiraAt, ip, userAgent);

        usuario.RegistrarAcceso(ahora);

        dbContext.Set<SesionUsuario>().Add(sesion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<LoginResultDto>.Success(new LoginResultDto
        {
            Token = token,
            ExpiraAt = expiraAt,
            Usuario = await ConstruirUsuarioSesionAsync(usuario, cancellationToken)
        });
    }

    public async Task<Result> LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Success();
        }

        var hash = HashToken(token);
        var sesion = await dbContext
            .Set<SesionUsuario>()
            .FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (sesion is null)
        {
            return Result.Success();
        }

        sesion.Revocar(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<UsuarioSesionDto>> ValidarSesionAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.NoAutorizado, "Sesion no valida.");
        }

        var hash = HashToken(token);
        var ahora = DateTimeOffset.UtcNow;

        var sesion = await dbContext
            .Set<SesionUsuario>()
            .AsNoTracking()
            .Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (sesion is null || !sesion.EstaActiva(ahora))
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.NoAutorizado, "Sesion expirada.");
        }

        if (!sesion.Usuario.EstaActivo || sesion.Usuario.EstaBloqueado(ahora))
        {
            return Result<UsuarioSesionDto>.Failure(AuthErrorCodes.NoAutorizado, "La cuenta no esta activa.");
        }

        return Result<UsuarioSesionDto>.Success(
            await ConstruirUsuarioSesionAsync(sesion.Usuario, cancellationToken));
    }

    public async Task<Result<RecuperacionResultDto>> SolicitarRecuperacionAsync(
        RecuperarPasswordDto solicitud,
        string resetUrlBase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        const string mensaje =
            "Si el correo pertenece a una cuenta activa, recibiras las instrucciones de recuperacion.";

        if (string.IsNullOrWhiteSpace(solicitud.Correo))
        {
            return Result<RecuperacionResultDto>.Failure(
                AuthErrorCodes.Validacion,
                "Escribe el correo de tu cuenta.");
        }

        var correo = Usuario.NormalizarCorreo(solicitud.Correo);
        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Correo == correo, cancellationToken);

        if (usuario is null || !usuario.EstaActivo)
        {
            return Result<RecuperacionResultDto>.Success(new RecuperacionResultDto { Mensaje = mensaje });
        }

        var token = GenerarToken();
        var expiraAt = DateTimeOffset.UtcNow.AddMinutes(_options.ResetTokenMinutes);
        var entidad = TokenRecuperacion.Crear(usuario.Id, HashToken(token), expiraAt);

        dbContext.Set<TokenRecuperacion>().Add(entidad);
        await dbContext.SaveChangesAsync(cancellationToken);

        var enlace = $"{resetUrlBase}?token={Uri.EscapeDataString(token)}";

        try
        {
            await emailSender.EnviarAsync(
                usuario.Correo,
                "Recupera tu acceso a Rescauta",
                $"<p>Hola {usuario.Nombre},</p><p>Usa este enlace para restablecer tu contrasena. Vence en {_options.ResetTokenMinutes} minutos.</p><p><a href=\"{enlace}\">Restablecer contrasena</a></p>",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el correo de recuperacion a {Correo}.", usuario.Correo);
        }

        return Result<RecuperacionResultDto>.Success(new RecuperacionResultDto
        {
            Mensaje = mensaje,
            EnlaceDesarrollo = _options.ExposeResetLinkInDevelopment ? enlace : null
        });
    }

    public async Task<Result> RestablecerPasswordAsync(
        RestablecerPasswordDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (string.IsNullOrWhiteSpace(solicitud.Token))
        {
            return Result.Failure(AuthErrorCodes.TokenInvalido, "El enlace de recuperacion no es valido.");
        }

        var error = ValidarPassword(solicitud.Password);
        if (error is not null)
        {
            return Result.Failure(AuthErrorCodes.PasswordDebil, error);
        }

        var hash = HashToken(solicitud.Token);
        var ahora = DateTimeOffset.UtcNow;
        var entidad = await dbContext
            .Set<TokenRecuperacion>()
            .Include(t => t.Usuario)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (entidad is null || !entidad.EsValido(ahora))
        {
            return Result.Failure(
                AuthErrorCodes.TokenInvalido,
                "El enlace vencio o ya fue utilizado. Solicita uno nuevo.");
        }

        entidad.Usuario.EstablecerPasswordHash(passwordHashService.Hash(solicitud.Password));
        entidad.MarcarUsado(ahora);

        var sesiones = await dbContext
            .Set<SesionUsuario>()
            .Where(s => s.UsuarioId == entidad.UsuarioId && s.RevocadaAt == null)
            .ToListAsync(cancellationToken);

        foreach (var sesion in sesiones)
        {
            sesion.Revocar(ahora);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> CambiarPasswordAsync(
        Guid usuarioId,
        CambiarPasswordDto solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var usuario = await dbContext
            .Set<Usuario>()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return Result.Failure(AuthErrorCodes.NoAutorizado, "La cuenta no existe.");
        }

        if (!passwordHashService.Verify(usuario.PasswordHash, solicitud.PasswordActual))
        {
            return Result.Failure(AuthErrorCodes.CredencialesInvalidas, "La contrasena actual no es correcta.");
        }

        var error = ValidarPassword(solicitud.PasswordNueva);
        if (error is not null)
        {
            return Result.Failure(AuthErrorCodes.PasswordDebil, error);
        }

        usuario.EstablecerPasswordHash(passwordHashService.Hash(solicitud.PasswordNueva));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<bool> CorreoExisteAsync(string correo, CancellationToken cancellationToken) =>
        await dbContext.Set<Usuario>().AnyAsync(u => u.Correo == correo, cancellationToken);

    private async Task<UsuarioSesionDto> ConstruirUsuarioSesionAsync(
        Usuario usuario,
        CancellationToken cancellationToken)
    {
        var comedores = await (
            from asignacion in dbContext.Set<AsignacionComedor>().AsNoTracking()
            join comedor in dbContext.Set<Comedor>().AsNoTracking()
                on asignacion.ComedorId equals comedor.Id
            where asignacion.UsuarioId == usuario.Id && asignacion.Activa
            select new ComedorAsignadoDto
            {
                Id = comedor.Id,
                Nombre = comedor.Nombre
            })
            .ToListAsync(cancellationToken);

        return new UsuarioSesionDto
        {
            UsuarioId = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Rol = usuario.Rol.ToString(),
            Estado = usuario.Estado.ToString(),
            Organizacion = usuario.Organizacion,
            Comedores = comedores.OrderBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList()
        };
    }

    private string? ValidarRegistro(string nombre, string correo, string password)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@', StringComparison.Ordinal))
        {
            return "Escribe un correo valido.";
        }

        return ValidarPassword(password);
    }

    private string? ValidarPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < _options.PasswordMinLength)
        {
            return $"La contrasena debe tener al menos {_options.PasswordMinLength} caracteres.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "La contrasena debe incluir al menos una letra y un numero.";
        }

        return null;
    }

    private static string GenerarToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
