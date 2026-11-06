using Rescauta.Application.Common;
using Rescauta.Application.Features.Donaciones.Dto;
using Rescauta.Application.Features.Kardex;

namespace Rescauta.Application.Features.Donaciones;

/// <summary>
/// Casos de uso de donacion: registro publico, consulta de seguimiento y gestion
/// administrativa por comedor.
/// </summary>
public interface IDonacionService
{
    /// <summary>
    /// Registra una donacion en estado Pendiente. NO mueve stock: la entrada al kardex se
    /// produce al confirmar la recepcion.
    /// </summary>
    Task<Result<DonacionResultDto>> RegistrarAsync(
        RegistrarDonacionDto donacion,
        Guid? usuarioId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Donaciones registradas, de la mas reciente a la mas antigua. El administrador ve
    /// todas; los demas roles solo las de sus comedores asignados.
    /// </summary>
    Task<Result<IReadOnlyList<DonacionResultDto>>> ObtenerAsync(
        UsuarioOperacion usuario,
        int limite = 50,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DonacionResultDto>>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        int limite = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca una donacion por su codigo de seguimiento. Es la lectura publica del modulo:
    /// el donante conoce su codigo, no un id interno.
    /// </summary>
    Task<Result<DonacionResultDto>> ObtenerPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);

    /// <summary>Listado administrativo paginado, filtrado por los comedores del usuario.</summary>
    Task<Result<PagedResult<DonacionResultDto>>> BuscarAsync(
        DonacionFiltroDto filtro,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    Task<Result<DonacionResultDto>> ObtenerPorIdAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DonacionHistorialDto>>> ObtenerHistorialAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    /// <summary>Confirma la recepcion y, mediante evento, asienta la entrada al kardex.</summary>
    Task<Result<DonacionResultDto>> ConfirmarAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    Task<Result<DonacionResultDto>> MarcarEnTransitoAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    Task<Result<DonacionResultDto>> EntregarAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    /// <summary>Cancela y revierte el stock si la donacion ya lo habia ingresado.</summary>
    Task<Result<DonacionResultDto>> CancelarAsync(
        Guid donacionId,
        MotivoDonacionDto motivo,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    /// <summary>Anula y revierte el stock si la donacion ya lo habia ingresado.</summary>
    Task<Result<DonacionResultDto>> AnularAsync(
        Guid donacionId,
        MotivoDonacionDto motivo,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    /// <summary>Borrado logico. Solo permitido si la donacion no movio stock.</summary>
    Task<Result> EliminarAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);
}
