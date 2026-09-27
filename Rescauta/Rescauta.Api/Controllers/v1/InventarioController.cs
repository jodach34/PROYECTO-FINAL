using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Application.Dtos;
using Rescauta.Application.Services;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Repositories;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints del Panel de Control: inventario por comedor y kardex de movimientos.
///
/// Este controller NO mueve stock. Solo traduce HTTP a llamadas de
/// <see cref="IKardexService"/>, que es donde vive la transaccionalidad. Que el
/// controlador no escriba en la base es deliberado: la regla de que el stock y su
/// movimiento se guardan juntos no se puede garantizar desde la capa de presentacion.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/inventario")]
[Produces("application/json")]
public sealed class InventarioController : ApiControllerBase
{
    private readonly IKardexRepository _kardex;
    private readonly IUrgenciaCalculadorService _urgencia;
    private readonly IKardexService _casoDeUso;

    public InventarioController(
        IKardexRepository kardex,
        IUrgenciaCalculadorService urgencia,
        IKardexService casoDeUso)
    {
        _kardex = kardex;
        _urgencia = urgencia;
        _casoDeUso = casoDeUso;
    }

    /// <summary>
    /// Inventario de un comedor, con los dias de stock restantes calculados.
    /// Es lo que pinta las tres tarjetas superiores del panel.
    /// </summary>
    [HttpGet("comedores/{comedorId:guid}/insumos")]
    [ProducesResponseType<IReadOnlyList<EvaluacionInsumoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluacionInsumoDto>>> ListarInsumosAsync(
        Guid comedorId,
        CancellationToken cancellationToken = default)
    {
        var insumos = await _kardex.ListarInsumosPorComedorAsync(comedorId, cancellationToken);

        return Ok(insumos.Select(insumo =>
        {
            var evaluacion = _urgencia.Evaluar(insumo);

            return new EvaluacionInsumoDto
            {
                Id = insumo.Id,
                Nombre = insumo.Nombre,
                Categoria = insumo.Categoria,
                UnidadMedida = insumo.UnidadMedida,
                StockActual = insumo.StockActual,
                StockMinimo = insumo.StockMinimo,
                DiasRestantes = evaluacion.DiasRestantes,
                Estado = evaluacion.Estado,
                CaducidadAprox = insumo.CaducidadAprox
            };
        }));
    }

    /// <summary>
    /// Kardex de un insumo, del movimiento mas antiguo al mas reciente.
    /// </summary>
    [HttpGet("insumos/{insumoId:guid}/kardex")]
    [ProducesResponseType<IReadOnlyList<MovimientoKardexDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoKardexDto>>> ListarKardexAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default)
    {
        var movimientos = await _kardex.ListarMovimientosPorInsumoAsync(insumoId, cancellationToken);

        return Ok(movimientos.Select(movimiento => ProyectarMovimiento(movimiento)));
    }

    /// <summary>
    /// Kardex reciente de todo el sistema: la tabla "Registro de Movimientos".
    /// </summary>
    [HttpGet("kardex/reciente")]
    [ProducesResponseType<IReadOnlyList<MovimientoKardexDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoKardexDto>>> ListarKardexRecienteAsync(
        [FromQuery] int cantidad = 20,
        CancellationToken cancellationToken = default) =>
        Ok((await _kardex.ListarMovimientosRecientesAsync(cantidad, cancellationToken))
            .Select(movimiento => ProyectarMovimiento(movimiento)));

    /// <summary>
    /// Registra una salida por consumo. Es el boton verde "Registrar Consumo Diario".
    ///
    /// No se toca el stock aqui: KardexService aplica la regla, escribe el renglon y
    /// confirma ambos en una sola transaccion. Los errores de negocio (stock
    /// insuficiente, cantidad invalida) llegan como DomainException y el middleware los
    /// traduce a 400 con el mensaje concreto.
    /// </summary>
    [HttpPost("insumos/{insumoId:guid}/consumo")]
    [ProducesResponseType<MovimientoKardexDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoKardexDto>> RegistrarConsumoAsync(
        Guid insumoId,
        [FromBody] RegistrarMovimientoRequest request,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _casoDeUso.RegistrarSalidaAsync(
            insumoId,
            request.Cantidad,
            request.Referencia,
            request.Responsable,
            cancellationToken);

        // Ruta explicita en vez de CreatedAtAction: el valor de version no viaja como
        // valor de ruta ambiental y el link generado quedaria 404.
        return Created(
            $"/api/v1/inventario/insumos/{insumoId}/kardex",
            ProyectarMovimiento(resultado));
    }

    /// <summary>
    /// Registra una entrada: ingreso de mercaderia al almacen del comedor. Es la
    /// contraparte del consumo y existia solo en el repositorio, sin endpoint.
    /// </summary>
    [HttpPost("insumos/{insumoId:guid}/entrada")]
    [ProducesResponseType<MovimientoKardexDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoKardexDto>> RegistrarEntradaAsync(
        Guid insumoId,
        [FromBody] RegistrarMovimientoRequest request,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _casoDeUso.RegistrarEntradaAsync(
            insumoId,
            request.Cantidad,
            request.Referencia,
            request.Responsable,
            cancellationToken);

        return Created(
            $"/api/v1/inventario/insumos/{insumoId}/kardex",
            ProyectarMovimiento(resultado));
    }

    /// <summary>
    /// Registra un ajuste por conteo fisico. <c>Cantidad</c> es el delta con signo:
    /// mandarlo en positivo suma (el conteo dio mas) y en negativo resta (merma).
    /// </summary>
    [HttpPost("insumos/{insumoId:guid}/ajuste")]
    [ProducesResponseType<MovimientoKardexDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoKardexDto>> RegistrarAjusteAsync(
        Guid insumoId,
        [FromBody] RegistrarMovimientoRequest request,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _casoDeUso.RegistrarAjusteAsync(
            insumoId,
            request.Cantidad,
            request.Referencia,
            request.Responsable,
            cancellationToken);

        return Created(
            $"/api/v1/inventario/insumos/{insumoId}/kardex",
            ProyectarMovimiento(resultado));
    }

    /// <summary>
    /// Recalcula el estado de abastecimiento de un insumo. Se expone separado del
    /// consumo porque el panel lo necesita solo, y recalcular es mas barato que un GET
    /// completo del inventario.
    /// </summary>
    [HttpGet("insumos/{insumoId:guid}/urgencia")]
    [ProducesResponseType<EvaluacionUrgencia>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluacionUrgencia>> EvaluarUrgenciaAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default)
    {
        var insumo = await _kardex.ObtenerInsumoAsync(insumoId, cancellationToken);

        return insumo is null
            ? NotFound()
            : Ok(_urgencia.Evaluar(insumo));
    }

    private static MovimientoKardexDto ProyectarMovimiento(KardexRegistroResult resultado) =>
        ProyectarMovimiento(
            resultado.Movimiento,
            resultado.InsumoNombre,
            resultado.UnidadMedida);

    private static MovimientoKardexDto ProyectarMovimiento(
        MovimientoKardex movimiento,
        string insumoNombre = "",
        string unidadMedida = "") => new()
    {
        Id = movimiento.Id,
        InsumoId = movimiento.InsumoId,
        InsumoNombre = insumoNombre,
        Tipo = movimiento.Tipo,
        Cantidad = movimiento.Cantidad,
        CantidadConSigno = movimiento.DeltaConSigno(),
        UnidadMedida = unidadMedida,
        StockResultante = movimiento.StockResultante,
        Fecha = movimiento.Fecha,
        Referencia = movimiento.Referencia,
        Responsable = movimiento.Responsable
    };
}

/// <summary>Cuerpo de <c>POST /inventario/insumos/{id}/consumo</c>.</summary>
public sealed record RegistrarMovimientoRequest
{
    /// <summary>Cantidad consumida, en la unidad de medida del insumo.</summary>
    public required decimal Cantidad { get; init; }

    /// <summary>Codigo de la donacion o nota asociada. Opcional.</summary>
    public string? Referencia { get; init; }

    /// <summary>Quien registro el consumo. Ej.: "Juana Quispe (Cocina)".</summary>
    public string? Responsable { get; init; }
}

/// <summary>
/// Insumo mas su evaluacion de urgencia. Es la fila de las tarjetas de metricas del
/// Panel de Control.
/// </summary>
public sealed record EvaluacionInsumoDto
{
    public required Guid Id { get; init; }

    public required string Nombre { get; init; }

    public required string Categoria { get; init; }

    public required string UnidadMedida { get; init; }

    public required decimal StockActual { get; init; }

    public required decimal StockMinimo { get; init; }

    /// <summary>Dias de cobertura que calcula UrgenciaCalculadorService.</summary>
    public required int DiasRestantes { get; init; }

    public required EstadoAbastecimiento Estado { get; init; }

    public DateOnly? CaducidadAprox { get; init; }
}
