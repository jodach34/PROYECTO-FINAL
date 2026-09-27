using Rescauta.Application.Common;
using Rescauta.Application.Features.Kardex.Dto;

namespace Rescauta.Application.Features.Kardex;

/// <summary>
/// Casos de uso del modulo de Inventario/Kardex.
///
/// Modulo del dev 2: Kardex, Inventario y Tiempo Real.
///
/// Contrato del servicio, no un handler de MediatR. El enunciado de la Fase 2 pide un
/// servicio, asi que se respeta. Ojo para el code review: Features/README.md pide un
/// handler por caso de uso con MediatR y FluentValidation, y esto se desvia de ahi. Motivo:
/// el servicio agrupa las operaciones de kardex y va a necesitar un notificador de
/// broadcasts, asi que una interfaz cohesiva tiene mas sentido que cinco handlers de una
/// linea. Si el equipo prefiere MediatR, se envuelve este servicio en un handler y el
/// contrato no cambia.
///
/// Pendiente para la siguiente iteracion: el validador FluentValidation de
/// <see cref="Dto.MovimientoKardexDto"/>. No se escribio en esta fase para no meter dos
/// capas de validacion de golpe; el servicio ya devuelve Result.Failure(Validacion) cuando
/// el dominio rechaza la entrada, de modo que el endpoint no queda sin proteccion mientras
/// tanto. El escaneo de AddValidatorsFromAssembly lo registran solo cuando exista la clase.
///
/// Todas las operaciones devuelven <see cref="Result{T}"/>, nunca lanzan por regla de
/// negocio (Features/README.md, regla 6). La API traduce el codigo de error a HTTP.
/// </summary>
public interface IKardexService
{
    /// <summary>
    /// Asienta una entrada o una salida en el kardex de un insumo.
    ///
    /// Rechaza la salida si la cantidad supera el stock disponible. Ante stock insuficiente
    /// devuelve <see cref="KardexErrorCodes.StockInsuficiente"/> y NO toca la base de datos.
    /// </summary>
    Task<Result<MovimientoKardexResultDto>> RegistrarMovimientoAsync(
        MovimientoKardexDto movimiento,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve el kardex de un insumo, del movimiento mas reciente al mas antiguo.
    /// Paginado porque un insumo con anios de historial no entra en un solo response.
    /// </summary>
    Task<Result<IReadOnlyList<MovimientoKardexResultDto>>> ObtenerKardexAsync(
        Guid insumoId,
        int pagina = 1,
        int tamanoPagina = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Stock actual de un insumo.</summary>
    Task<Result<decimal>> ObtenerStockAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emite una alerta de emergencia en tiempo real. No persiste nada: es el canal para
    /// que el operador en turno se entere ya, no un asiento del kardex.
    /// </summary>
    Task<Result> EnviarAlertaEmergenciaAsync(
        AlertaEmergenciaDto alerta,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Codigos de error del modulo. El frontend decide que mostrar a partir del codigo, no
/// del texto del mensaje, asi que son contrato: cambiar uno es un cambio rompiente.
/// </summary>
public static class KardexErrorCodes
{
    /// <summary>El insumo no existe o esta dado de baja.</summary>
    public const string InsumoNoEncontrado = "kardex.insumo_no_encontrado";

    /// <summary>La salida pedida supera el stock disponible.</summary>
    public const string StockInsuficiente = "kardex.stock_insuficiente";

    /// <summary>La peticion no paso las validaciones de forma (FluentValidation).</summary>
    public const string Validacion = "kardex.validacion";

    /// <summary>
    /// La escritura choco con otra operacion concurrente. El cliente debe reintentar con
    /// backoff, no volver a insentar a ciegas.
    /// </summary>
    public const string ConflictoConcurrente = "kardex.conflicto_concurrente";

    /// <summary>Fallo la base de datos o la cache. Es un 500, no un error de negocio.</summary>
    public const string ErrorInterno = "kardex.error_interno";
}
