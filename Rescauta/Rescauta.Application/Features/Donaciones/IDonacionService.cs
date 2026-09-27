using Rescauta.Application.Common;
using Rescauta.Application.Features.Donaciones.Dto;

namespace Rescauta.Application.Features.Donaciones;

/// <summary>
/// Casos de uso de donacion: lo que entra por el formulario del Banco de Alimentos.
/// </summary>
public interface IDonacionService
{
    /// <summary>
    /// Registra una donacion y asienta la entrada correspondiente en el kardex del insumo.
    /// Son las dos cosas en una sola operacion: una donacion que se perdio, o un asiento sin
    /// la donacion que lo justifico.
    /// </summary>
    Task<Result<DonacionResultDto>> RegistrarAsync(
        RegistrarDonacionDto donacion,
        CancellationToken cancellationToken = default);

    /// <summary>Donaciones registradas, de la mas reciente a la mas antigua.</summary>
    Task<Result<IReadOnlyList<DonacionResultDto>>> ObtenerAsync(
        int limite = 50,
        CancellationToken cancellationToken = default);
}
