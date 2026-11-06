using Rescauta.Application.Common;
using Rescauta.Application.Features.Kardex;
using Rescauta.Application.Features.Reportes.Dto;

namespace Rescauta.Application.Features.Reportes;

/// <summary>
/// Casos de uso de reportes basicos: donaciones recibidas y stock disponible.
/// El alcance por comedor lo resuelve cada metodo con el <see cref="UsuarioOperacion"/>.
/// </summary>
public interface IReporteService
{
    Task<Result<ReporteDonacionesRecibidasDto>> ObtenerDonacionesRecibidasAsync(
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ReporteStockInsumoDto>>> ObtenerStockDisponibleAsync(
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default);
}
