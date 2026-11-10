using Rescauta.Application.Common;
using Rescauta.Application.Features.Productos.Dto;

namespace Rescauta.Application.Interfaces;

/// <summary>
/// Consulta un producto por su codigo de barras en Open Food Facts. Vive en Application
/// como abstraccion porque la fuente externa concreta es un detalle de Infrastructure.
/// </summary>
public interface IOpenFoodFactsClient
{
    Task<Result<ProductoBarcodeDto>> BuscarPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);
}
