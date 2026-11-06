using Rescauta.Application.Common;
using Rescauta.Application.Features.Autenticacion.Dto;

namespace Rescauta.Application.Features.Autenticacion;

public interface IAutenticacionService
{
    Task<Result<UsuarioSesionDto>> RegistrarDonanteAsync(
        RegistrarDonanteDto registro,
        CancellationToken cancellationToken = default);

    Task<Result<UsuarioSesionDto>> SolicitarOperadorAsync(
        SolicitarOperadorDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result<LoginResultDto>> LoginAsync(
        LoginDto login,
        string? ip,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(string token, CancellationToken cancellationToken = default);

    Task<Result<UsuarioSesionDto>> ValidarSesionAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<Result<RecuperacionResultDto>> SolicitarRecuperacionAsync(
        RecuperarPasswordDto solicitud,
        string resetUrlBase,
        CancellationToken cancellationToken = default);

    Task<Result> RestablecerPasswordAsync(
        RestablecerPasswordDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result> CambiarPasswordAsync(
        Guid usuarioId,
        CambiarPasswordDto solicitud,
        CancellationToken cancellationToken = default);
}
