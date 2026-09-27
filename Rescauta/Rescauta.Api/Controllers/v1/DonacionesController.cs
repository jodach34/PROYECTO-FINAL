using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Application.Features.Donaciones;
using Rescauta.Application.Features.Donaciones.Dto;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints de donacion. Son los que consume el formulario del Banco de Alimentos.
///
///   GET  /api/v1/donaciones          lista las donaciones registradas
///   POST /api/v1/donaciones          registra una donacion y asienta la entrada en kardex
///
/// La entidad <c>Donacion</c> ya existia en el dominio pero no tenia controller: el modulo
/// estaba a medio hacer. Este controller es el que la cierra.
///
/// El POST devuelve 201 con la donacion YA confirmada, no un 202: el asiento del kardex se
/// escribe en la misma transaccion, asi que cuando el cliente recibe la respuesta la
/// mercancia ya esta en la despensa del comedor.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class DonacionesController(IDonacionService donacionService) : ApiControllerBase
{
    /// <summary>Donaciones registradas, de la mas reciente a la mas antigua.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DonacionResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DonacionResultDto>>> Listar(
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        return FromResult(await donacionService.ObtenerAsync(limite, cancellationToken));
    }

    /// <summary>
    /// Registra la donacion. El servidor genera el codigo de seguimiento y lo devuelve: el
    /// cliente no lo manda, porque es el indice unico de la tabla.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<DonacionResultDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DonacionResultDto>> Registrar(
        [FromBody] RegistrarDonacionDto donacion,
        CancellationToken cancellationToken)
    {
        var result = await donacionService.RegistrarAsync(donacion, cancellationToken);

        if (result.IsFailure)
        {
            return FromResult(result);
        }

        // CreatedAtAction y no Created: el recurso devuelto no tiene endpoint propio para
        // leer una donacion por id, asi que la Location apuntaria a algo que da 404.
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
