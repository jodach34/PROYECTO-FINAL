using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Controllers;
using Rescauta.Application.Features.Kardex;
using Rescauta.Application.Features.Kardex.Dto;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Endpoints del modulo de Inventario/Kardex.
///
///   POST /api/v1/kardex/movimiento     asienta una entrada o una salida
///   POST /api/v1/kardex/sync-offline   reintenta en lote los movimientos pendientes
///   GET  /api/v1/kardex/insumos/{id}/stock   saldo de un insumo
///
/// El controller NO escribe reglas de negocio: traduce HTTP a caso de uso y Result a HTTP.
/// Toda la validacion vive en el agregado Insumo, que es el unico que puede mover el saldo.
///
/// Rutas: el enunciado de la fase pide /api/kardex/..., pero el cascaron versiona las
/// APIs con /api/v{version}/. Se sigue la convencion del proyecto: las rutas reales son
/// /api/v1/kardex/... Un cliente que llame a /api/kardex/movimiento recibira 404.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class KardexController(IKardexService kardexService) : ApiControllerBase
{
    /// <summary>
    /// Asienta un movimiento en el kardex de un insumo.
    ///
    /// 200 con el asiento y el saldo resultante. 400 si el dominio lo rechaza, por ejemplo
    /// cuando una salida supera el stock disponible.
    /// </summary>
    [HttpPost("movimiento")]
    [ProducesResponseType<MovimientoKardexResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<MovimientoKardexResultDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MovimientoKardexResultDto>> RegistrarMovimiento(
        [FromBody] MovimientoKardexDto movimiento,
        CancellationToken cancellationToken)
    {
        var result = await kardexService.RegistrarMovimientoAsync(movimiento, cancellationToken);

        return FromResult(result);
    }

    /// <summary>
    /// Reintenta en lote los movimientos que el dispositivo acumulo sin conexion.
    ///
    /// Cada elemento se procesa por separado y se devuelven los dos resultados: los que
    /// entraron y los que rebotaron con su motivo. Es lo que permite al operador ver que
    /// paso con lo que el dispositivo acumulo en la zona sin cobertura, en vez de recibir
    /// un unico "error" sin detalle.
    ///
    /// Devuelve 200 aunque haya fallos parciales: un lote con 3 de 10 movimientos
    /// rechazados NO es un error del endpoint, es el resultado del caso de uso. El codigo
    /// HTTP de error se reserva para que el lote entero no se pueda procesar.
    /// </summary>
    [HttpPost("sync-offline")]
    [ProducesResponseType<SyncOfflineKardexResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SyncOfflineKardexResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SyncOfflineKardexResponse>> SincronizarOffline(
        [FromBody] IEnumerable<MovimientoKardexDto> movimientos,
        CancellationToken cancellationToken)
    {
        if (movimientos is null || !movimientos.Any())
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: KardexErrorCodes.Validacion,
                detail: "La lista de movimientos no puede estar vacia.");
        }

        // Se materializa porque la lista se ordena y despues se recorre dos veces
        // (procesar y luego contar). Un IEnumerable se reevalua en cada recorrido, y con
        // una consulta de base de datos debajo eso serian dos consultas distintas.
        var lote = movimientos.ToList();

        if (lote.Count > 500)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: KardexErrorCodes.Validacion,
                detail: $"El lote no puede superar los 500 movimientos. Recibidos: {lote.Count}.");
        }

        // ORDEN POR FECHA, obligatorio. El kardex es una secuencia: si el dispositivo
        // registro una entrada y despues una salida sobre el mismo insumo, aplicar la
        // salida primero la rechazaria por stock insuficiente aunque la entrada se hubiera
        // asentado bien. Ordenar es lo que hace que el lote sea equivalente a los
        // movimientos aplicados uno por uno en su momento.
        var ordenados = lote
            .OrderBy(x => x.FechaHora ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.TipoMovimiento)
            .ToList();

        var exitosos = new List<MovimientoKardexResultDto>(ordenados.Count);
        var fallidos = new List<MovimientoKardexErrorDto>(ordenados.Count);

        // Secuencial a proposito, NO Task.WhenAll. Dos salidas concurrentes sobre el mismo
        // insumo darian DatabaseUpdateConcurrencyException (el RowVersion lo detecta) y
        // el lote fallaria entero, cuando en realidad es una cuestion de orden. Ademas
        // cada movimiento abre su propia transaccion.
        foreach (var movimiento in ordenados)
        {
            var result = await kardexService.RegistrarMovimientoAsync(movimiento, cancellationToken);

            if (result.IsSuccess)
            {
                exitosos.Add(result.Value);
            }
            else
            {
                fallidos.Add(new MovimientoKardexErrorDto
                {
                    InsumoId = movimiento.InsumoId,
                    TipoMovimiento = movimiento.TipoMovimiento,
                    Cantidad = movimiento.Cantidad,
                    FechaHora = movimiento.FechaHora,
                    CodigoError = result.ErrorCode ?? KardexErrorCodes.ErrorInterno,
                    Mensaje = result.ErrorMessage ?? "No se pudo asentar el movimiento."
                });
            }
        }

        return Ok(new SyncOfflineKardexResponse
        {
            TotalRecibidos = ordenados.Count,
            TotalExitosos = exitosos.Count,
            TotalFallidos = fallidos.Count,
            ProcesadosCompletamente = fallidos.Count == 0,
            Exitosos = exitosos,
            Fallidos = fallidos
        });
    }

    /// <summary>
    /// Saldo actual de un insumo. El caso de uso (ObtenerStockAsync) ya existia desde el
    /// principio pero no lo exponia nadie, asi que el cliente no tenia forma de leer un
    /// saldo sin asentar un movimiento. Este endpoint es esa lectura.
    ///
    /// 404 si el insumo no existe, 400 solo si llega un id vacio.
    /// </summary>
    [HttpGet("insumos/{insumoId:guid}/stock")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerStock(
        Guid insumoId,
        CancellationToken cancellationToken)
    {
        var result = await kardexService.ObtenerStockAsync(insumoId, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = result.ErrorCode ?? "No encontrado",
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(new { insumoId, stockActual = result.Value });
    }
}

/// <summary>
/// Resumen de un lote de sincronizacion offline. <c>ProcesadosCompletamente</c> es lo que el
/// cliente mira para decidir si borra su cola local: si es false, debe conservar los
/// movimientos fallidos para reintentarlos, no asumirlos perdidos.
/// </summary>
public sealed record SyncOfflineKardexResponse
{
    /// <summary>Movimientos que llegaron en el lote, ya ordenados por fecha.</summary>
    public int TotalRecibidos { get; init; }

    public int TotalExitosos { get; init; }

    public int TotalFallidos { get; init; }

    /// <summary>True si no hubo ni un solo rechazo.</summary>
    public bool ProcesadosCompletamente { get; init; }

    public IReadOnlyList<MovimientoKardexResultDto> Exitosos { get; init; } = [];

    /// <summary>Los que rebotaron, con el motivo de cada uno.</summary>
    public IReadOnlyList<MovimientoKardexErrorDto> Fallidos { get; init; } = [];
}

/// <summary>
/// Un movimiento que no se pudo asentar. Incluye los datos que el cliente ya conoce, para
/// que pueda emparejarlo con su cola local sin tener que comparar por posicion: el lote
/// puede venir reordenado.
/// </summary>
public sealed record MovimientoKardexErrorDto
{
    public Guid InsumoId { get; init; }
    public TipoMovimiento TipoMovimiento { get; init; }
    public decimal Cantidad { get; init; }
    public DateTimeOffset? FechaHora { get; init; }

    /// <summary>Codigo de negocio, por ejemplo kardex.stock_insuficiente.</summary>
    public string CodigoError { get; init; } = string.Empty;

    /// <summary>Motivo legible para mostrar al operador.</summary>
    public string Mensaje { get; init; } = string.Empty;
}
