using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Api.Security;
using Rescauta.Application.Features.Productos.Dto;
using Rescauta.Application.Interfaces;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Consulta de productos externos para el alta de insumos. No escribe en la base ni
/// participa del flujo de Kardex: solo traduce el codigo de barras a datos editables.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = AuthPolicies.Operacion)]
public sealed class ProductosController(IOpenFoodFactsClient openFoodFacts) : ApiControllerBase
{
    [HttpGet("barcode/{codigo}")]
    [ProducesResponseType<ProductoBarcodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> BuscarPorCodigo(
        string codigo,
        CancellationToken cancellationToken)
    {
        var resultado = await openFoodFacts.BuscarPorCodigoAsync(codigo, cancellationToken);

        if (resultado.IsSuccess)
        {
            return Ok(resultado.Value);
        }

        var estado = resultado.ErrorCode switch
        {
            ProductoErrorCodes.CodigoInvalido => StatusCodes.Status400BadRequest,
            ProductoErrorCodes.NoEncontrado => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status502BadGateway
        };

        return Problem(
            statusCode: estado,
            title: resultado.ErrorCode,
            detail: resultado.ErrorMessage,
            instance: HttpContext.Request.Path);
    }
}
