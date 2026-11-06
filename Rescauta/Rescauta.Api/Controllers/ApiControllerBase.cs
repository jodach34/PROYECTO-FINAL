using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Kardex;

namespace Rescauta.Api.Controllers;

/// <summary>
/// Base de todos los controllers de Rescauta.
///
/// Reglas para los 3 devs:
///   * Un controller por modulo y por version, en Controllers/v1/:
///       Controllers/v1/MapasController.cs
///       Controllers/v1/KardexController.cs
///       Controllers/v1/DonacionesController.cs
///     El atributo [Route("api/v{version:apiVersion}")] y [ApiVersion("1.0")] ya son
///     parte del cascarón: heredar de esta base evita olvidos de versionado.
///   * El controller NO escribe reglas de negocio. Solo traduce HTTP -> caso de uso
///     y resultado -> HTTP.
///   * Nada de logica aqui. Si un controller supera ~30 lineas, casi todo esta en el
///     lugar equivocado.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Traduce un <see cref="Result"/> de Application a una respuesta HTTP coherente.
    /// Evita que cada endpoint invente su propio formato de error.
    /// </summary>
    protected ActionResult<TResponse> FromResult<TResponse>(Result<TResponse> result)
        => result.IsSuccess
            ? Ok(result.Value)
            : ProblemFrom(result.ErrorCode, result.ErrorMessage);

    /// <summary>Traduce un <see cref="Result"/> sin valor de retorno.</summary>
    protected ActionResult FromResult(Result result)
        => result.IsSuccess
            ? NoContent()
            : ProblemFrom(result.ErrorCode, result.ErrorMessage);

    /// <summary>
    /// Identidad de operacion a partir de los claims de la sesion. La usan los endpoints
    /// de Kardex para resolver que comedores puede consultar u operar el usuario.
    /// </summary>
    protected UsuarioOperacion? UsuarioActual()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return null;
        }

        var comedores = User
            .FindAll("comedor_id")
            .Select(claim => Guid.TryParse(claim.Value, out var comedorId) ? comedorId : Guid.Empty)
            .Where(comedorId => comedorId != Guid.Empty)
            .Distinct()
            .ToList();

        return new UsuarioOperacion(
            usuarioId,
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            comedores);
    }

    private ObjectResult ProblemFrom(string? errorCode, string? errorMessage)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = errorCode ?? "Error",
            Detail = errorMessage,
            Instance = HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return BadRequest(problem);
    }
}
