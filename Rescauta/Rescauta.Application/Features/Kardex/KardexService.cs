using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Kardex.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.Caching;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Kardex;

/// <summary>
/// Implementacion de los casos de uso de kardex.
///
/// Dependencias y por que:
///   * <see cref="IAppDbContext"/> y no AppDbContext: la capa Application no referencia
///     Infrastructure. Se resuelve el agregado con Set&lt;Insumo&gt;(), sin tocar la
///     abstraccion compartida y sin colisionar con los otros modulos.
///   * <see cref="IInventoryNotifier"/> y no IHubContext: idem, sin dependencia de SignalR.
///   * <see cref="ICacheService"/> para invalidar el resumen de inventario por prefijo
///     (regla del modulo: rescauta:kardex:).
///
/// REGLA CENTRAL DEL METODO RegistrarMovimientoAsync: el saldo NUNCA se toca a mano. Se
/// llama a un metodo de negocio del agregado, que valida, asienta el movimiento y moves el
/// saldo en la misma operacion. Si este servicio hiciera
/// <c>insumo.StockActual -= cantidad</c>, la invariante "el saldo es la suma de los
/// movimientos" dejaria de ser cierto y el inventario podria descuadrarse sin que nadie lo
/// note.
/// </summary>
public sealed class KardexService(
    IAppDbContext dbContext,
    IInventoryNotifier notifier,
    ICacheService cacheService,
    ILogger<KardexService> logger) : IKardexService
{
    /// <summary>Prefijo de cache del modulo. Regla 3 de Features/Kardex/README.md.</summary>
    private const string CachePrefix = "rescauta:kardex:";

    /// <summary>
    /// Margen por debajo del cual el stock se considera de riesgo. No es una regla de
    /// negocio dura: es el umbral que dispara la alerta al canal de emergencia. Ajustable
    /// por almacen en una fase posterior; hoy es una constante.
    /// </summary>
    private const decimal UmbralAlertaStockBajo = 5m;

    public async Task<Result<MovimientoKardexResultDto>> RegistrarMovimientoAsync(
        MovimientoKardexDto movimiento,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movimiento);

        // 1. Cargar el agregado. AsNoTracking NO se usa: el agregado tiene que quedar
        //    tracked para que EF vea los cambios de StockActual y detecte el movimiento
        //    hijo agregado en la coleccion.
        var insumo = await dbContext
            .Set<Insumo>()
            .FirstOrDefaultAsync(x => x.Id == movimiento.InsumoId, cancellationToken);

        if (insumo is null)
        {
            return Result<MovimientoKardexResultDto>.Failure(
                KardexErrorCodes.InsumoNoEncontrado,
                $"No existe un insumo con id {movimiento.InsumoId}.");
        }

        // 2. Saldo antes de tocar nada. Se usa para el payload de tiempo real (el cliente
        //    quiere el diff) y para el mensaje de error de stock insuficiente.
        var stockPrevio = insumo.StockActual;

        MovimientoKardex asiento;
        try
        {
            // 3. LA REGLA DE NEGOCIO. El agregado es quien valida:
            //    - la cantidad tiene que ser mayor que cero;
            //    - una Salida no puede superar el StockActual;
            //    - responsable y concepto son obligatorios.
            //    Si la cantidad es mayor que el stock, AsentarMovimiento lanza
            //    DomainException y no se escribe nada. La validacion NO se reimplementa
            //    aqui a mano: duplicarla es la forma clasica de que las dos copias se
            //    desincronicen.
            asiento = movimiento.TipoMovimiento switch
            {
                TipoMovimiento.Entrada => insumo.RegistrarEntrada(
                    movimiento.Cantidad,
                    movimiento.Responsable,
                    movimiento.Concepto,
                    movimiento.FechaHora),

                TipoMovimiento.Salida => insumo.RegistrarSalida(
                    movimiento.Cantidad,
                    movimiento.Responsable,
                    movimiento.Concepto,
                    movimiento.FechaHora),

                _ => throw new DomainException(
                    $"Tipo de movimiento no soportado: {movimiento.TipoMovimiento}.",
                    nameof(movimiento))
            };
        }
        catch (DomainException ex)
        {
            // Excepcion de negocio esperada: se traduce a Result, no sube como 500.
            logger.LogWarning(
                "Movimiento de kardex rechazado. Insumo={InsumoId}, Tipo={Tipo}, Cantidad={Cantidad}. Motivo: {Motivo}",
                movimiento.InsumoId,
                movimiento.TipoMovimiento,
                movimiento.Cantidad,
                ex.Message);

            var codigo = EsStockInsuficiente(ex, movimiento, stockPrevio)
                ? KardexErrorCodes.StockInsuficiente
                : KardexErrorCodes.Validacion;

            return Result<MovimientoKardexResultDto>.Failure(codigo, ex.Message);
        }

        // 4. Registrar el asiento a mano. La coleccion del agregado es de solo lectura, asi
        //    que EF no puede deducir de ella que hay un asiento NUEVO: lo da por una entidad
        //    ya asentada, lo manda como UPDATE contra una fila que no existe, el UPDATE
        //    afecta cero filas y SaveChangesAsync revienta con DbUpdateConcurrencyException,
        //    que este servicio reportaba despues como "otro operacion actualizo este
        //    insumo". No habia ninguna concurrencia: el asiento no se habia insertado nunca.
        //
        //    El saldo NO necesita el mismo trato: es una propiedad escalar y EF la compara
        //    contra su fotografia sin problema, porque el agregado se cargo con una consulta.
        dbContext.Set<MovimientoKardex>().Add(asiento);

        // 5. Persistir. Un unico SaveChangesAsync: el asiento y el saldo se escriben en la
        //    misma transaccion implicita de EF, o se escriben los dos o ninguno. Un
        //    movimiento sin su saldo, o un saldo sin su movimiento, descuadra el kardex.
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Dos salidas simultaneas sobre el mismo insumo: la segunda leyo un stock que la
            // primera ya gasto. El RowVersion de BaseEntity lo detecta. No es culpa del
            // cliente: se le dice que reintente.
            logger.LogWarning(
                ex,
                "Conflicto de concurrencia al asentar un movimiento. Insumo={InsumoId}",
                movimiento.InsumoId);

            return Result<MovimientoKardexResultDto>.Failure(
                KardexErrorCodes.ConflictoConcurrente,
                "Otro operacion actualizo este insumo al mismo tiempo. Reintente la operacion.");
        }

        // 5. Invalidar cache. El resumen de inventario y el saldo cacheado quedaron
        //    desactualizados. Por prefijo, nunca por clave global (regla del modulo).
        await InvalidarCacheAsync(cancellationToken);

        // 6. Tiempo real, DESPUES de confirmar la escritura. Al reves, un fallo del broker
        //    dejaria clientes viendo un saldo que la base nunca confirmo.
        var actualizacion = new StockActualizadoDto
        {
            InsumoId = insumo.Id,
            Nombre = insumo.Nombre,
            UnidadMedida = insumo.UnidadMedida,
            StockActual = insumo.StockActual,
            StockPrevio = stockPrevio,
            MovimientoId = asiento.Id,
            TipoMovimiento = asiento.TipoMovimiento,
            Cantidad = asiento.Cantidad,
            FechaHora = asiento.FechaHora
        };

        await notificarConRobustezAsync(
            () => notifier.NotificarStockActualizadoAsync(actualizacion, cancellationToken),
            $"stock de {insumo.Nombre} tras {asiento.TipoMovimiento}",
            cancellationToken);

        // 7. Alerta de emergencia si el saldo quedo en zona de riesgo. Solo para salidas: una
        //    entrada nunca deja al almacen mas pobre.
        if (movimiento.TipoMovimiento == TipoMovimiento.Salida &&
            insumo.StockActual <= UmbralAlertaStockBajo)
        {
            var alerta = new AlertaEmergenciaDto
            {
                InsumoId = insumo.Id,
                Nombre = insumo.Nombre,
                StockActual = insumo.StockActual,
                Mensaje =
                    $"Stock critico de {insumo.Nombre}: quedan {insumo.StockActual} " +
                    $"{insumo.UnidadMedida} tras una salida de {asiento.Cantidad}.",
                Nivel = "critico"
            };

            await notificarConRobustezAsync(
                () => notifier.EnviarAlertaEmergenciaAsync(alerta, cancellationToken),
                $"alerta de stock critico de {insumo.Nombre}",
                cancellationToken);
        }

        var resultado = new MovimientoKardexResultDto
        {
            MovimientoId = asiento.Id,
            InsumoId = insumo.Id,
            StockActual = insumo.StockActual,
            UnidadMedida = insumo.UnidadMedida,
            FechaHora = asiento.FechaHora,
            TipoMovimiento = asiento.TipoMovimiento
        };

        logger.LogInformation(
            "Movimiento de kardex asentado. Insumo={InsumoId}, Movimiento={MovimientoId}, Tipo={Tipo}, Cantidad={Cantidad}, StockPrevio={StockPrevio}, StockActual={StockActual}",
            insumo.Id,
            asiento.Id,
            asiento.TipoMovimiento,
            asiento.Cantidad,
            stockPrevio,
            insumo.StockActual);

        return Result<MovimientoKardexResultDto>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<MovimientoKardexResultDto>>> ObtenerKardexAsync(
        Guid insumoId,
        int pagina = 1,
        int tamanoPagina = 50,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1)
        {
            return Result<IReadOnlyList<MovimientoKardexResultDto>>.Failure(
                KardexErrorCodes.Validacion,
                "La pagina debe ser mayor o igual a 1.");
        }

        if (tamanoPagina is < 1 or > 200)
        {
            return Result<IReadOnlyList<MovimientoKardexResultDto>>.Failure(
                KardexErrorCodes.Validacion,
                "El tamano de pagina debe estar entre 1 y 200.");
        }

        // AsNoTracking aqui si: es solo lectura y el historial puede ser largo.
        var movimientos = await dbContext
            .Set<MovimientoKardex>()
            .AsNoTracking()
            .Where(x => x.InsumoId == insumoId)
            .OrderByDescending(x => x.FechaHora)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(x => new MovimientoKardexResultDto
            {
                MovimientoId = x.Id,
                InsumoId = x.InsumoId,
                StockActual = 0m,
                UnidadMedida = string.Empty,
                FechaHora = x.FechaHora,
                TipoMovimiento = x.TipoMovimiento
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<MovimientoKardexResultDto>>.Success(movimientos);
    }

    public async Task<Result<decimal>> ObtenerStockAsync(
        Guid insumoId,
        CancellationToken cancellationToken = default)
    {
        // Proyeccion a decimal directo: se pide solo la columna, no la entidad entera.
        var stock = await dbContext
            .Set<Insumo>()
            .AsNoTracking()
            .Where(x => x.Id == insumoId)
            .Select(x => (decimal?)x.StockActual)
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            return Result<decimal>.Failure(
                KardexErrorCodes.InsumoNoEncontrado,
                $"No existe un insumo con id {insumoId}.");
        }

        return Result<decimal>.Success(stock.Value);
    }

    public async Task<Result> EnviarAlertaEmergenciaAsync(
        AlertaEmergenciaDto alerta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alerta);

        if (string.IsNullOrWhiteSpace(alerta.Mensaje))
        {
            return Result.Failure(
                KardexErrorCodes.Validacion,
                "La alerta debe traer un mensaje legible para el operador.");
        }

        try
        {
            await notifier.EnviarAlertaEmergenciaAsync(alerta, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Una alerta es best-effort: si el canal cae, se registra y se sigue. Perder la
            // notificacion no puede tumbar la peticion ni el proceso.
            logger.LogError(ex, "No se pudo emitir la alerta de emergencia");
            return Result.Failure(
                KardexErrorCodes.ErrorInterno,
                "No se pudo emitir la alerta de emergencia.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Traduce la excepcion del dominio al codigo de error de stock insuficiente. Se
    /// distingue por el tipo de movimiento y no por comparar el texto del mensaje: si el
    /// texto cambia, el codigo de error no.
    /// </summary>
    private static bool EsStockInsuficiente(
        DomainException ex,
        MovimientoKardexDto movimiento,
        decimal stockPrevio)
    {
        return movimiento.TipoMovimiento == TipoMovimiento.Salida &&
               movimiento.Cantidad > stockPrevio &&
               ex.Errors.Count == 0;
    }

    private async Task InvalidarCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cacheService.RemoveByPrefixAsync(CachePrefix, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La cache es una aceleracion, no la fuente de verdad. Si no se puede
            // invalidar, el asiento ya esta escrito y se acepta un TTL vencido.
            logger.LogWarning(ex, "No se pudo invalidar la cache de kardex");
        }
    }

    /// <summary>
    /// Emite un evento de tiempo real sin dejar que un fallo del transporte tumbe la
    /// operacion de negocio. El movimiento ya quedo persistido: devolver un error aqui
    /// seria mentir al cliente, que si se guardo.
    /// </summary>
    private async Task notificarConRobustezAsync(
        Func<Task> envio,
        string descripcion,
        CancellationToken cancellationToken)
    {
        try
        {
            await envio();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Movimiento persistido pero no se pudo emitir el evento de tiempo real: {Descripcion}",
                descripcion);
        }
    }
}
