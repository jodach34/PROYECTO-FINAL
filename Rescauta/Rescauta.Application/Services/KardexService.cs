using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Services;

/// <inheritdoc cref="IKardexService" />
public sealed class KardexService : IKardexService
{
    /// <summary>
    /// Evento de SignalR emitido tras cada movimiento confirmado. Lo fija el modulo de
    /// inventario; cambiarlo rompe a los clientes ya suscritos.
    /// </summary>
    private const string EventoMovimientoRegistrado = "inventario.movimiento.registrado";

    private readonly IAppDbContext _dbContext;
    private readonly IUrgenciaCalculadorService _urgencia;
    private readonly IRescautaNotifier _notificador;

    public KardexService(
        IAppDbContext dbContext,
        IUrgenciaCalculadorService urgencia,
        IRescautaNotifier notificador)
    {
        _dbContext = dbContext;
        _urgencia = urgencia;
        _notificador = notificador;
    }

    public Task<KardexRegistroResult> RegistrarEntradaAsync(
        Guid insumoId,
        decimal cantidad,
        string? referencia = null,
        string? responsable = null,
        CancellationToken cancellationToken = default) =>
        RegistrarAsync(insumoId, TipoMovimientoKardex.Entrada, cantidad, referencia, responsable, cancellationToken);

    public Task<KardexRegistroResult> RegistrarSalidaAsync(
        Guid insumoId,
        decimal cantidad,
        string? referencia = null,
        string? responsable = null,
        CancellationToken cancellationToken = default) =>
        RegistrarAsync(insumoId, TipoMovimientoKardex.Salida, cantidad, referencia, responsable, cancellationToken);

    public Task<KardexRegistroResult> RegistrarAjusteAsync(
        Guid insumoId,
        decimal delta,
        string? referencia = null,
        string? responsable = null,
        CancellationToken cancellationToken = default) =>
        RegistrarAsync(insumoId, TipoMovimientoKardex.Ajuste, delta, referencia, responsable, cancellationToken);

    /// <summary>
    /// Nucleo de los tres casos de uso: aplica la regla de dominio, escribe el renglon y
    /// confirma todo en una sola transaccion.
    /// </summary>
    private async Task<KardexRegistroResult> RegistrarAsync(
        Guid insumoId,
        TipoMovimientoKardex tipo,
        decimal cantidad,
        string? referencia,
        string? responsable,
        CancellationToken cancellationToken)
    {
        // Se carga SEGUIDO (sin AsNoTracking) a proposito: al venir trackeada, mutar
        // StockActual dentro de la entidad la marca como Modified y el unico
        // SaveChanges de mas abajo la persiste. Con AsNoTracking el cambio se perderia
        // en silencio, que es exactamente el bug que este caso de uso reemplaza.
        var insumo = await _dbContext.Set<Insumo>()
            .FirstOrDefaultAsync(insumo => insumo.Id == insumoId, cancellationToken)
            // KeyNotFoundException la traduce el middleware a 404.
            ?? throw new KeyNotFoundException(
                $"No existe un insumo con id {insumoId}.");

        // La regla de negocio vive en la entidad, no aqui. Si la cantidad no es valida
        // o no hay stock, la entidad lanza DomainException y el middleware la devuelve
        // como 400 con el mensaje concreto ("Stock insuficiente: se piden 30 kg y solo
        // hay 12"), que es lo que necesita quien esta en la cocina.
        var stockResultante = tipo switch
        {
            TipoMovimientoKardex.Entrada => insumo.RegistrarEntrada(cantidad),
            TipoMovimientoKardex.Salida => insumo.RegistrarSalida(cantidad),
            TipoMovimientoKardex.Ajuste => insumo.AjustarPorDelta(cantidad),
            _ => throw new DomainException($"Tipo de movimiento no soportado: {tipo}.")
        };

        var movimiento = new MovimientoKardex(
            insumoId,
            tipo,
            cantidad,
            stockResultante,
            DateTimeOffset.UtcNow,
            referencia,
            responsable);

        await _dbContext.Set<MovimientoKardex>().AddAsync(movimiento, cancellationToken);

        // UN SOLO SaveChanges = UNA TRANSACCION. Cubre a la vez el UPDATE del stock y
        // el INSERT del movimiento. Si el INSERT fallara, el stock se revierte; antes,
        // con el guardado dentro del repositorio, el movimiento quedaba escrito y el
        // stock no se movia, y el endpoint devolvia 201 sin haber hecho nada.
        await _dbContext.SaveChangesAsync(cancellationToken);

        var estado = _urgencia.EvaluarEstado(
            insumo.StockActual,
            insumo.StockMinimo,
            insumo.ConsumoDiarioEstimado);

        // La notificacion va DESPUES del commit, nunca antes: todavia no se confirmo nada
        // cuando se avisa, un suscriptor podria leer un stock que todavia no existe.
        // Si SignalR falla, el movimiento ya esta confirmado y solo se pierde el aviso.
        await _notificador.SendUpdateAsync(
            EventoMovimientoRegistrado,
            new
            {
                movimientoId = movimiento.Id,
                insumoId = movimiento.InsumoId,
                insumoNombre = insumo.Nombre,
                tipo = movimiento.Tipo.ToString(),
                cantidad = movimiento.Cantidad,
                deltaConSigno = movimiento.DeltaConSigno(),
                unidadMedida = insumo.UnidadMedida,
                stockResultante = movimiento.StockResultante,
                estado = estado.ToString()
            },
            cancellationToken);

        return new KardexRegistroResult(
            movimiento,
            insumo.Nombre,
            insumo.UnidadMedida,
            estado);
    }
}
