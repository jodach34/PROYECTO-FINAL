using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Application.Dtos;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Repositories;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints del Banco de Alimentos: registrar una donacion, ver los comedores
/// recomendados y generar el QR de seguimiento.
///
/// Lo delicate de este modulo es el cruce con inventario. Recordar la regla: este
/// controller NUNCA escribe un movimiento de kardex. Al asignar la donacion se publica
/// <c>DonacionConfirmada</c> y es un handler de Application el que crea la entrada de
/// inventario. Si alguna vez se anade una llamada directa a IKardexRepository desde
/// aqui, la dependencia circular entre los dos modulos queda creada.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/donaciones")]
[Produces("application/json")]
public sealed class DonacionesController : ApiControllerBase
{
    private readonly IDonacionRepository _donaciones;
    private readonly IQrGeneratorService _qr;
    private readonly IAppDbContext _dbContext;

    public DonacionesController(
        IDonacionRepository donaciones,
        IQrGeneratorService qr,
        IAppDbContext dbContext)
    {
        _donaciones = donaciones;
        _qr = qr;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Comedores que el sistema recomienda para la donacion, del mas urgente al mas
    /// abundante. Es la tabla de la columna derecha del Banco de Alimentos.
    ///
    /// La distancia se devuelve en cero porque todavia no hay servicio de rutas; el DTO
    /// ya tiene el campo para que el frontend no tenga que cambiar cuando exista.
    /// </summary>
    [HttpGet("comedores-recomendados")]
    [ProducesResponseType<IReadOnlyList<ComedorRecomendadoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComedorRecomendadoDto>>> ListarComedoresRecomendadosAsync(
        CancellationToken cancellationToken = default)
    {
        var comedores = await _donaciones.ListarComedoresRecomendadosAsync(cancellationToken);

        return Ok(comedores.Select(comedor => new ComedorRecomendadoDto
        {
            Id = comedor.Id,
            Nombre = comedor.Nombre,
            Distrito = comedor.Distrito,
            Urgencia = comedor.Estado,
            DistanciaKm = 0d,
            RacionesDiarias = comedor.RacionesDiarias
        }));
    }

    /// <summary>
    /// Registra una donacion y devuelve el QR con su codigo de seguimiento. Es el boton
    /// verde "Confirmar Donacion y Generar Codigo de Seguimiento".
    ///
    /// El codigo se genera en el servidor y se valida contra la base antes de insertar.
    /// Si dos donantes pulsan a la vez, el indice unico de
    /// DonacionConfiguration es la red de seguridad final.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DonacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DonacionDto>> RegistrarAsync(
        [FromBody] RegistrarDonacionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoSeguimiento))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Datos invalidos",
                Detail = "El codigo de seguimiento es obligatorio.",
                Instance = HttpContext.Request.Path
            });
        }

        var codigo = request.CodigoSeguimiento.Trim().ToUpperInvariant();

        if (await _donaciones.ExisteCodigoAsync(codigo, cancellationToken))
        {
            // 409 y no 400: el codigo esta bien formado, lo que choca es que ya existe.
            // El frontend puede ofrecer generar otro sin limpiar el formulario.
            return Conflict(new ProblemDetails
            {
                Title = "Codigo de seguimiento duplicado",
                Detail = $"El codigo '{codigo}' ya esta registrado en otra donacion.",
                Instance = HttpContext.Request.Path
            });
        }

        var donacion = new Donacion(
            codigo,
            request.NombreProducto,
            request.Categoria,
            request.Cantidad,
            request.UnidadMedida,
            request.DireccionRecojo,
            request.CaducidadAprox);

        await _donaciones.AgregarAsync(donacion, cancellationToken);

        var qr = await _qr.GenerarQrDonacionAsync(donacion.CodigoSeguimiento, cancellationToken);

        // CreatedAtAction no resuelve la ruta aca porque el valor de version no se
        // propaga como valor de ruta ambiental y el link queda 404. Se construye la
        // ruta a mano: es explicito y no depende de la resolucion de nombres.
        return Created($"/api/v1/donaciones/codigo/{Uri.EscapeDataString(donacion.CodigoSeguimiento)}",
            new DonacionDto
            {
                Id = donacion.Id,
                CodigoSeguimiento = donacion.CodigoSeguimiento,
                NombreProducto = donacion.NombreProducto,
                Categoria = donacion.Categoria,
                Cantidad = donacion.Cantidad,
                UnidadMedida = donacion.UnidadMedida,
                CaducidadAprox = donacion.CaducidadAprox,
                DireccionRecojo = donacion.DireccionRecojo,
                Estado = donacion.Estado,
                QrDataUri = qr
            });
    }

    /// <summary>
    /// Consulta una donacion por su codigo de seguimiento. Es lo que se escanea desde
    /// el QR en el punto de recojo.
    /// </summary>
    [HttpGet("codigo/{codigo}")]
    [ProducesResponseType<DonacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonacionDto>> ObtenerPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        var donacion = await _donaciones.ObtenerPorCodigoAsync(codigo, cancellationToken);

        if (donacion is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Donacion no encontrada",
                Detail = $"No existe ninguna donacion con el codigo '{codigo}'.",
                Instance = HttpContext.Request.Path
            });
        }

        var qr = await _qr.GenerarQrDonacionAsync(donacion.CodigoSeguimiento, cancellationToken);

        return Ok(new DonacionDto
        {
            Id = donacion.Id,
            CodigoSeguimiento = donacion.CodigoSeguimiento,
            NombreProducto = donacion.NombreProducto,
            Categoria = donacion.Categoria,
            Cantidad = donacion.Cantidad,
            UnidadMedida = donacion.UnidadMedida,
            CaducidadAprox = donacion.CaducidadAprox,
            DireccionRecojo = donacion.DireccionRecojo,
            Estado = donacion.Estado,
            ComedorAsignadoId = donacion.ComedorAsignadoId,
            QrDataUri = qr
        });
    }

    /// <summary>
    /// Revisa la donacion y confirma que el insumo puede entrar al inventario. Es el
    /// paso intermedio obligatorio entre registrarla y asignarla.
    ///
    /// Sin este endpoint el flujo estaba roto: <c>AsignarAComedor</c> exige el estado
    /// <c>Clasificada</c> y las donaciones nacen en <c>Recibida</c>, asi que asignar
    /// fallaba siempre con 400 y no habia forma de llegar al estado intermedio.
    /// </summary>
    [HttpPost("{id:guid}/clasificar")]
    [ProducesResponseType<DonacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonacionDto>> ClasificarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var donacion = await _donaciones.ObtenerPorIdAsync(id, cancellationToken);

        if (donacion is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Donacion no encontrada",
                Detail = $"No existe una donacion con id {id}.",
                Instance = HttpContext.Request.Path
            });
        }

        donacion.Clasificar();
        _donaciones.Actualizar(donacion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await ProyectarAsync(donacion, cancellationToken));
    }

    /// <summary>
    /// Asigna la donacion a un comedor. Es el paso que dispara
    /// <c>DonacionConfirmada</c> y, con el, la entrada de inventario.
    /// </summary>
    [HttpPost("{id:guid}/asignar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AsignarAsync(
        Guid id,
        [FromBody] AsignarDonacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var donacion = await _donaciones.ObtenerPorIdAsync(id, cancellationToken);

        if (donacion is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Donacion no encontrada",
                Detail = $"No existe una donacion con id {id}.",
                Instance = HttpContext.Request.Path
            });
        }

        // DomainException por transicion invalida (asignar dos veces, o saltar la
        // clasificacion) la traduce ExceptionHandlingMiddleware a 400 con el mensaje.
        donacion.AsignarAComedor(request.ComedorId);
        _donaciones.Actualizar(donacion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Proyecta la donacion al DTO de salida, incluido el QR regenerado.</summary>
    private async Task<DonacionDto> ProyectarAsync(Donacion donacion, CancellationToken cancellationToken)
    {
        var qr = await _qr.GenerarQrDonacionAsync(donacion.CodigoSeguimiento, cancellationToken);

        return new DonacionDto
        {
            Id = donacion.Id,
            CodigoSeguimiento = donacion.CodigoSeguimiento,
            NombreProducto = donacion.NombreProducto,
            Categoria = donacion.Categoria,
            Cantidad = donacion.Cantidad,
            UnidadMedida = donacion.UnidadMedida,
            CaducidadAprox = donacion.CaducidadAprox,
            DireccionRecojo = donacion.DireccionRecojo,
            Estado = donacion.Estado,
            ComedorAsignadoId = donacion.ComedorAsignadoId,
            QrDataUri = qr
        };
    }
}

/// <summary>Cuerpo de <c>POST /donaciones</c>.</summary>
public sealed record RegistrarDonacionRequest
{
    /// <summary>Codigo legible que ira en el QR. Ej.: "DON-2026-00421".</summary>
    public required string CodigoSeguimiento { get; init; }

    /// <summary>Ej.: "Sacos de Arroz Costeño de 50kg".</summary>
    public required string NombreProducto { get; init; }

    /// <summary>Ej.: "Alimentos Secos (Arroz, Menestras, Conservas)".</summary>
    public required string Categoria { get; init; }

    public required decimal Cantidad { get; init; }

    /// <summary>Ej.: "Unidades (Sacos de 50kg)".</summary>
    public required string UnidadMedida { get; init; }

    public DateOnly? CaducidadAprox { get; init; }

    public required string DireccionRecojo { get; init; }
}

/// <summary>Cuerpo de <c>POST /donaciones/{id}/asignar</c>.</summary>
public sealed record AsignarDonacionRequest
{
    public required Guid ComedorId { get; init; }
}
