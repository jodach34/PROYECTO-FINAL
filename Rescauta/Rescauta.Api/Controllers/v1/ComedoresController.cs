using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Application.Dtos;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Repositories;
using Rescauta.Domain.ValueObjects;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints del mapa interactivo: el listado de comedores con sus filtros y el
/// detalle de uno solo.
///
/// El controller no calcula nada de negocio. Solo traduce HTTP a una llamada de
/// repositorio y el resultado a un <see cref="ComedorDto"/>. Si alguna vez hace falta
/// mas, la logica va en un caso de uso de Application.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/comedores")]
[Produces("application/json")]
public sealed class ComedoresController : ApiControllerBase
{
    private readonly IComedorRepository _comedores;

    public ComedoresController(IComedorRepository comedores)
    {
        _comedores = comedores;
    }

    /// <summary>
    /// Lista los comedores con los filtros del sidebar.
    /// Con <c>urgencia=true</c> devuelve solo los que el mapa tiene que pintar de rojo.
    /// </summary>
    /// <param name="nombre">Filtro parcial por nombre ("Filtrar por nombre...").</param>
    /// <param name="estado">Filtro exacto por estado de abastecimiento.</param>
    /// <param name="distrito">Filtro exacto por distrito.</param>
    /// <param name="urgencia">Si es true, ordena de mayor a menor urgencia.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ComedorDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComedorDto>>> ListarAsync(
        [FromQuery] string? nombre,
        [FromQuery] EstadoAbastecimiento? estado,
        [FromQuery] string? distrito,
        [FromQuery] bool urgencia = false,
        CancellationToken cancellationToken = default)
    {
        var comedores = urgencia
            ? await _comedores.ListarPorUrgenciaAsync(cancellationToken)
            : await _comedores.ListarPorFiltrosAsync(nombre, estado, distrito, cancellationToken);

        return Ok(comedores.Select(Proyectar));
    }

    /// <summary>Detalle de un comedor, para la ficha flotante del mapa.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ComedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComedorDto>> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var comedor = await _comedores.ObtenerPorIdAsync(id, cancellationToken);

        // 404 explicito y no un null silencioso: un null en un ActionResult<T> se
        // convierte en 204 sin cuerpo, y el frontend no puede distinguir "no existe" de
        // "la peticion salio bien pero no hay contenido".
        return comedor is null
            ? NotFound(new ProblemDetails
            {
                Title = "Comedor no encontrado",
                Detail = $"No existe un comedor con id {id}.",
                Instance = HttpContext.Request.Path
            })
            : Ok(Proyectar(comedor));
    }

    /// <summary>
    /// Proyecta entidad a DTO. El value object <see cref="UbicacionGeo"/> se aplana a
    /// dos campos porque el mapa de Leaflet espera lat y lng sueltos, no un objeto.
    /// </summary>
    private static ComedorDto Proyectar(Domain.Entities.Comedor comedor) => new()
    {
        Id = comedor.Id,
        Nombre = comedor.Nombre,
        Distrito = comedor.Distrito,
        Direccion = comedor.Direccion,
        ContactoDirecto = comedor.ContactoDirecto,
        RacionesDiarias = comedor.RacionesDiarias,
        Estado = comedor.Estado,
        Latitud = comedor.Ubicacion?.Latitud,
        Longitud = comedor.Ubicacion?.Longitud
    };
}
