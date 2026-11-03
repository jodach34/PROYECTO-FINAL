using Rescauta.Application.Common;
using Rescauta.Application.Features.Usuarios.Dto;

namespace Rescauta.Application.Features.Usuarios;

public interface IUsuarioAdminService
{
    Task<Result<IReadOnlyList<UsuarioAdminDto>>> ListarUsuariosAsync(
        string? busqueda = null,
        CancellationToken cancellationToken = default);

    Task<Result<UsuarioAdminDto>> ObtenerUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SolicitudOperadorAdminDto>>> ListarSolicitudesPendientesAsync(
        CancellationToken cancellationToken = default);

    Task<Result> AprobarOperadorAsync(
        Guid usuarioId,
        Guid administradorId,
        AprobarOperadorDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result> RechazarSolicitudAsync(
        Guid usuarioId,
        Guid administradorId,
        RechazarSolicitudDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result> CambiarRolAsync(
        Guid usuarioId,
        Guid administradorId,
        CambiarRolDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result> CambiarEstadoAsync(
        Guid usuarioId,
        Guid administradorId,
        CambiarEstadoUsuarioDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result> ActualizarAsignacionesAsync(
        Guid usuarioId,
        ActualizarAsignacionesDto solicitud,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ComedorOpcionDto>>> ListarComedoresAsync(
        CancellationToken cancellationToken = default);
}
