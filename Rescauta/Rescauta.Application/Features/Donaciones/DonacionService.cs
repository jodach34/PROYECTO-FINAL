using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Donaciones.Dto;
using Rescauta.Application.Features.Donaciones.Events;
using Rescauta.Application.Features.Kardex;
using Rescauta.Application.Features.Kardex.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Application.Interfaces.Caching;
using Rescauta.Application.Interfaces.RealTime;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Donaciones;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Donaciones;

/// <summary>
/// Casos de uso de donacion.
///
/// REGLA CENTRAL: registrar una donacion NO mueve stock. La donacion nace Pendiente y la
/// entrada al kardex ocurre al confirmar la recepcion, cuando la coordinacion verifica que
/// los insumos llegaron fisicamente. Cancelar o anular una donacion que ya ingreso stock
/// publica un evento de reversion.
///
/// El movimiento de stock lo hace el handler del evento, no este servicio. Asi Donaciones
/// no conoce las entidades de Kardex y se cumple la regla del README del modulo: los dos
/// modulos se comunican por eventos, no por llamadas directas.
/// </summary>
public sealed class DonacionService(
    IAppDbContext dbContext,
    IPublisher publisher,
    InventarioDonacionResultado inventarioResultado,
    ICacheService cacheService,
    IInventoryNotifier notifier,
    IValidator<RegistrarDonacionDto> registrarValidator,
    IValidator<MotivoDonacionDto> motivoValidator,
    ILogger<DonacionService> logger) : IDonacionService
{
    private const string PrefijoCodigo = "DON";
    private const string CacheDonaciones = "rescauta:donaciones:";
    private const string CacheKardex = "rescauta:kardex:";
    private const int LimiteListado = 200;

    public async Task<Result<DonacionResultDto>> RegistrarAsync(
        RegistrarDonacionDto donacion,
        Guid? usuarioId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(donacion);

        var validacion = await registrarValidator.ValidateAsync(donacion, cancellationToken);
        if (!validacion.IsValid)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                FormatearErrores(validacion));
        }

        // AsNoTracking: registrar no modifica el insumo. El stock recien se mueve al
        // confirmar, y ese handler carga su propia instancia tracked.
        var insumo = await dbContext
            .Set<Insumo>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == donacion.InsumoId && !x.IsDeleted,
                cancellationToken);

        if (insumo is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.InsumoNoEncontrado,
                $"No existe un insumo con id {donacion.InsumoId}.");
        }

        var comedor = await dbContext
            .Set<Comedor>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == insumo.ComedorId && !x.IsDeleted,
                cancellationToken);

        if (comedor is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.InsumoNoEncontrado,
                $"El insumo {insumo.Nombre} no pertenece a ningun comedor activo.");
        }

        Donacion entidad;
        try
        {
            entidad = Donacion.Registrar(
                insumo.Id,
                donacion.Cantidad,
                donacion.Donante,
                GenerarCodigoSeguimiento(),
                comedor.Id,
                insumo.Nombre,
                insumo.UnidadMedida,
                comedor.Nombre,
                puntoRecojo: donacion.PuntoRecojo,
                fechaDonacion: null,
                usuarioId: usuarioId);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(
                "Donacion rechazada. Insumo={InsumoId}, Cantidad={Cantidad}. Motivo: {Motivo}",
                donacion.InsumoId,
                donacion.Cantidad,
                ex.Message);

            return Result<DonacionResultDto>.Failure(DonacionErrorCodes.Validacion, ex.Message);
        }

        dbContext.Set<Donacion>().Add(entidad);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnico(ex))
        {
            logger.LogWarning(ex, "Colision del codigo de seguimiento de la donacion.");

            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.ErrorInterno,
                "No se pudo generar un codigo de seguimiento unico. Reintente.");
        }

        await InvalidarCacheAsync(cancellationToken);

        var resultado = Mapear(entidad, insumo.StockActual);

        logger.LogInformation(
            "Donacion registrada en estado Pendiente. Codigo={Codigo}, Comedor={Comedor}, " +
            "Insumo={Insumo}, Cantidad={Cantidad}",
            entidad.CodigoSeguimiento,
            comedor.Nombre,
            insumo.Nombre,
            entidad.Cantidad);

        return Result<DonacionResultDto>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<DonacionResultDto>>> ObtenerAsync(
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        if (limite is < 1 or > LimiteListado)
        {
            return Result<IReadOnlyList<DonacionResultDto>>.Failure(
                DonacionErrorCodes.Validacion,
                $"El limite debe estar entre 1 y {LimiteListado}.");
        }

        var filas = await dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.FechaDonacionUtc)
            .ThenByDescending(d => d.CreatedAt)
            .Take(limite)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DonacionResultDto>>.Success(
            filas.Select(d => Mapear(d, 0m)).ToList());
    }

    public async Task<Result<IReadOnlyList<DonacionResultDto>>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty)
        {
            return Result<IReadOnlyList<DonacionResultDto>>.Failure(
                DonacionErrorCodes.Validacion,
                "El usuario es obligatorio.");
        }

        if (limite is < 1 or > LimiteListado)
        {
            return Result<IReadOnlyList<DonacionResultDto>>.Failure(
                DonacionErrorCodes.Validacion,
                $"El limite debe estar entre 1 y {LimiteListado}.");
        }

        var filas = await dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .Where(d => !d.IsDeleted && d.UsuarioId == usuarioId)
            .OrderByDescending(d => d.FechaDonacionUtc)
            .ThenByDescending(d => d.CreatedAt)
            .Take(limite)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DonacionResultDto>>.Success(
            filas.Select(d => Mapear(d, 0m)).ToList());
    }

    public async Task<Result<DonacionResultDto>> ObtenerPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                "Escribe el codigo de seguimiento de tu donacion.");
        }

        var normalizado = codigo.Trim().ToUpperInvariant();

        var entidad = await dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.CodigoSeguimiento == normalizado && !d.IsDeleted,
                cancellationToken);

        if (entidad is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.DonacionNoEncontrada,
                $"No encontramos una donacion con el codigo {normalizado}. " +
                "Revisa que este bien escrito o contacta a la coordinacion.");
        }

        var stock = await ObtenerStockAsync(entidad.InsumoId, cancellationToken);

        return Result<DonacionResultDto>.Success(Mapear(entidad, stock));
    }

    public async Task<Result<PagedResult<DonacionResultDto>>> BuscarAsync(
        DonacionFiltroDto filtro,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(usuario);

        if (filtro.Pagina < 1)
        {
            return Result<PagedResult<DonacionResultDto>>.Failure(
                DonacionErrorCodes.Validacion,
                "La pagina debe ser mayor o igual a 1.");
        }

        if (filtro.TamanoPagina is < 1 or > 100)
        {
            return Result<PagedResult<DonacionResultDto>>.Failure(
                DonacionErrorCodes.Validacion,
                "El tamano de pagina debe estar entre 1 y 100.");
        }

        if (filtro.ComedorId is { } comedorSolicitado && !usuario.PuedeOperar(comedorSolicitado))
        {
            return Result<PagedResult<DonacionResultDto>>.Failure(
                DonacionErrorCodes.SinPermiso,
                "No tienes permisos para consultar las donaciones de ese comedor.");
        }

        var consulta = dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .Where(d => !d.IsDeleted);

        if (!usuario.EsAdministrador)
        {
            var comedores = usuario.ComedoresAsignados.ToArray();

            if (comedores.Length == 0)
            {
                return Result<PagedResult<DonacionResultDto>>.Success(
                    PagedResult<DonacionResultDto>.Create(
                        [],
                        0,
                        filtro.Pagina,
                        filtro.TamanoPagina));
            }

            consulta = consulta.Where(d => comedores.Contains(d.ComedorId));
        }

        if (filtro.ComedorId is { } comedorId)
        {
            consulta = consulta.Where(d => d.ComedorId == comedorId);
        }

        if (filtro.InsumoId is { } insumoId)
        {
            consulta = consulta.Where(d => d.InsumoId == insumoId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(d => d.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var termino = filtro.Busqueda.Trim().ToUpperInvariant();
            consulta = consulta.Where(d =>
                d.CodigoSeguimiento.Contains(termino) ||
                d.Donante.ToUpper().Contains(termino));
        }

        if (filtro.Desde is { } desde)
        {
            var desdeUtc = desde.UtcDateTime;
            consulta = consulta.Where(d => d.FechaDonacionUtc >= desdeUtc);
        }

        if (filtro.Hasta is { } hasta)
        {
            var hastaUtc = hasta.UtcDateTime;
            consulta = consulta.Where(d => d.FechaDonacionUtc <= hastaUtc);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var filas = await consulta
            .OrderByDescending(d => d.FechaDonacionUtc)
            .ThenByDescending(d => d.CreatedAt)
            .Skip((filtro.Pagina - 1) * filtro.TamanoPagina)
            .Take(filtro.TamanoPagina)
            .ToListAsync(cancellationToken);

        var items = filas.Select(d => Mapear(d, 0m)).ToList();

        return Result<PagedResult<DonacionResultDto>>.Success(
            PagedResult<DonacionResultDto>.Create(
                items,
                total,
                filtro.Pagina,
                filtro.TamanoPagina));
    }

    public async Task<Result<DonacionResultDto>> ObtenerPorIdAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var entidad = await dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == donacionId && !d.IsDeleted,
                cancellationToken);

        if (entidad is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.DonacionNoEncontrada,
                "No existe la donacion solicitada.");
        }

        if (!usuario.PuedeOperar(entidad.ComedorId))
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.SinPermiso,
                "No tienes permisos para consultar esta donacion.");
        }

        var stock = await ObtenerStockAsync(entidad.InsumoId, cancellationToken);

        return Result<DonacionResultDto>.Success(Mapear(entidad, stock));
    }

    public async Task<Result<IReadOnlyList<DonacionHistorialDto>>> ObtenerHistorialAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var donacion = await dbContext
            .Set<Donacion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == donacionId && !d.IsDeleted,
                cancellationToken);

        if (donacion is null)
        {
            return Result<IReadOnlyList<DonacionHistorialDto>>.Failure(
                DonacionErrorCodes.DonacionNoEncontrada,
                "No existe la donacion solicitada.");
        }

        if (!usuario.PuedeOperar(donacion.ComedorId))
        {
            return Result<IReadOnlyList<DonacionHistorialDto>>.Failure(
                DonacionErrorCodes.SinPermiso,
                "No tienes permisos para consultar esta donacion.");
        }

        var filas = await dbContext
            .Set<DonacionHistorial>()
            .AsNoTracking()
            .Where(h => h.DonacionId == donacionId)
            .OrderBy(h => h.Fecha)
            .ToListAsync(cancellationToken);

        var historial = filas
            .Select(h => new DonacionHistorialDto
            {
                EstadoAnterior = h.EstadoAnterior.ToString(),
                EstadoNuevo = h.EstadoNuevo.ToString(),
                Motivo = h.Motivo,
                UsuarioId = h.UsuarioId,
                Fecha = h.Fecha
            })
            .ToList();

        return Result<IReadOnlyList<DonacionHistorialDto>>.Success(historial);
    }

    public Task<Result<DonacionResultDto>> ConfirmarAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        return EjecutarTransicionAsync(
            donacionId,
            usuario,
            "confirmar",
            donacion => donacion.ConfirmarRecepcion(),
            reversionDeStock: false,
            cancellationToken);
    }

    public Task<Result<DonacionResultDto>> MarcarEnTransitoAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        return EjecutarTransicionAsync(
            donacionId,
            usuario,
            "marcar en transito",
            donacion => donacion.MarcarEnTransito(),
            reversionDeStock: false,
            cancellationToken);
    }

    public Task<Result<DonacionResultDto>> EntregarAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        return EjecutarTransicionAsync(
            donacionId,
            usuario,
            "entregar",
            donacion => donacion.MarcarEntregada(),
            reversionDeStock: false,
            cancellationToken);
    }

    public async Task<Result<DonacionResultDto>> CancelarAsync(
        Guid donacionId,
        MotivoDonacionDto motivo,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(motivo);

        var validacion = await motivoValidator.ValidateAsync(motivo, cancellationToken);
        if (!validacion.IsValid)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                FormatearErrores(validacion));
        }

        return await EjecutarTransicionAsync(
            donacionId,
            usuario,
            "cancelar",
            donacion => donacion.Cancelar(motivo.Motivo),
            reversionDeStock: true,
            cancellationToken);
    }

    public async Task<Result<DonacionResultDto>> AnularAsync(
        Guid donacionId,
        MotivoDonacionDto motivo,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(motivo);

        var validacion = await motivoValidator.ValidateAsync(motivo, cancellationToken);
        if (!validacion.IsValid)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                FormatearErrores(validacion));
        }

        return await EjecutarTransicionAsync(
            donacionId,
            usuario,
            "anular",
            donacion => donacion.Anular(motivo.Motivo),
            reversionDeStock: true,
            cancellationToken);
    }

    public async Task<Result> EliminarAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var donacion = await dbContext
            .Set<Donacion>()
            .FirstOrDefaultAsync(
                d => d.Id == donacionId && !d.IsDeleted,
                cancellationToken);

        if (donacion is null)
        {
            return Result.Failure(
                DonacionErrorCodes.DonacionNoEncontrada,
                "No existe la donacion solicitada.");
        }

        if (!usuario.PuedeOperar(donacion.ComedorId))
        {
            return Result.Failure(
                DonacionErrorCodes.SinPermiso,
                "No tienes permisos para eliminar esta donacion.");
        }

        try
        {
            donacion.MarcarComoEliminada();
        }
        catch (DomainException ex)
        {
            return Result.Failure(DonacionErrorCodes.EstadoInvalido, ex.Message);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Conflicto al eliminar donacion {DonacionId}", donacionId);

            return Result.Failure(
                DonacionErrorCodes.ConflictoConcurrente,
                "Otro usuario actualizo esta donacion. Reintente.");
        }

        await InvalidarCacheAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<DonacionResultDto>> EjecutarTransicionAsync(
        Guid donacionId,
        UsuarioOperacion usuario,
        string accion,
        Action<Donacion> transicion,
        bool reversionDeStock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var donacion = await dbContext
            .Set<Donacion>()
            .FirstOrDefaultAsync(
                d => d.Id == donacionId && !d.IsDeleted,
                cancellationToken);

        if (donacion is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.DonacionNoEncontrada,
                "No existe la donacion solicitada.");
        }

        if (!usuario.PuedeOperar(donacion.ComedorId))
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.SinPermiso,
                "No tienes permisos para operar sobre esta donacion.");
        }

        var estadoAnterior = donacion.Estado;
        var moviaStock = donacion.StockIngresado;
        inventarioResultado.Reiniciar();

        try
        {
            transicion(donacion);

            if (donacion.Estado == EstadoDonacion.Confirmada)
            {
                await publisher.Publish(
                    CrearEventoConfirmacion(donacion),
                    cancellationToken);
            }
            else if (reversionDeStock && moviaStock)
            {
                await publisher.Publish(
                    new DonacionStockRevertido(
                        donacion.Id,
                        donacion.CodigoSeguimiento,
                        donacion.InsumoId,
                        donacion.ComedorId,
                        donacion.InsumoNombre,
                        donacion.UnidadMedida,
                        donacion.Cantidad,
                        donacion.MotivoCierre ?? "Sin motivo",
                        donacion.FechaCierre ?? DateTimeOffset.UtcNow),
                    cancellationToken);
            }
        }
        catch (DomainException ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo {Accion} la donacion {DonacionId}. Estado actual: {Estado}",
                accion,
                donacionId,
                estadoAnterior);

            var codigo = reversionDeStock && ex.Message.Contains(
                "stock suficiente",
                StringComparison.OrdinalIgnoreCase)
                ? DonacionErrorCodes.StockInsuficiente
                : DonacionErrorCodes.EstadoInvalido;

            return Result<DonacionResultDto>.Failure(codigo, ex.Message);
        }

        var fechaCambio = ObtenerFechaCambio(donacion);

        dbContext.Set<DonacionHistorial>().Add(
            DonacionHistorial.Crear(
                donacion.Id,
                estadoAnterior,
                donacion.Estado,
                donacion.MotivoCierre,
                usuario.UsuarioId,
                fechaCambio));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(
                ex,
                "Conflicto de concurrencia al {Accion} la donacion {DonacionId}",
                accion,
                donacionId);

            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.ConflictoConcurrente,
                "Otro usuario actualizo esta donacion al mismo tiempo. Reintente.");
        }
        catch (DbUpdateException ex) when (reversionDeStock)
        {
            logger.LogWarning(
                ex,
                "No se pudo revertir el stock de la donacion {DonacionId}",
                donacionId);

            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.ErrorInterno,
                "No se pudo revertir el stock de la donacion. Reintente.");
        }

        await InvalidarCacheAsync(cancellationToken);

        var stock = inventarioResultado.HuboMovimiento
            ? inventarioResultado.StockActual
            : await ObtenerStockAsync(donacion.InsumoId, cancellationToken);

        if (inventarioResultado.HuboMovimiento)
        {
            await NotificarStockAsync(cancellationToken);
        }

        logger.LogInformation(
            "Donacion {Accion}. Codigo={Codigo}, EstadoAnterior={EstadoAnterior}, EstadoNuevo={EstadoNuevo}",
            accion,
            donacion.CodigoSeguimiento,
            estadoAnterior,
            donacion.Estado);

        return Result<DonacionResultDto>.Success(Mapear(donacion, stock));
    }

    private static DonacionConfirmada CrearEventoConfirmacion(Donacion donacion) =>
        new(
            donacion.Id,
            donacion.CodigoSeguimiento,
            donacion.InsumoId,
            donacion.ComedorId,
            donacion.InsumoNombre,
            donacion.UnidadMedida,
            donacion.ComedorNombre,
            donacion.Cantidad,
            donacion.Donante,
            donacion.PuntoRecojo,
            donacion.FechaConfirmacion ?? DateTimeOffset.UtcNow);

    private static DateTimeOffset ObtenerFechaCambio(Donacion donacion) =>
        donacion.FechaCierre ??
        donacion.FechaEntrega ??
        donacion.FechaEnTransito ??
        donacion.FechaConfirmacion ??
        DateTimeOffset.UtcNow;

    private async Task<decimal> ObtenerStockAsync(
        Guid insumoId,
        CancellationToken cancellationToken)
    {
        return await dbContext
            .Set<Insumo>()
            .AsNoTracking()
            .Where(i => i.Id == insumoId)
            .Select(i => i.StockActual)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DonacionResultDto Mapear(Donacion donacion, decimal stock) =>
        new()
        {
            DonacionId = donacion.Id,
            CodigoSeguimiento = donacion.CodigoSeguimiento,
            InsumoId = donacion.InsumoId,
            InsumoNombre = donacion.InsumoNombre,
            ComedorId = donacion.ComedorId,
            ComedorNombre = donacion.ComedorNombre,
            Cantidad = donacion.Cantidad,
            UnidadMedida = donacion.UnidadMedida,
            Donante = donacion.Donante,
            PuntoRecojo = donacion.PuntoRecojo,
            Estado = donacion.Estado.ToString(),
            FechaDonacion = donacion.FechaDonacion,
            FechaConfirmacion = donacion.FechaConfirmacion,
            FechaEnTransito = donacion.FechaEnTransito,
            FechaEntrega = donacion.FechaEntrega,
            FechaCierre = donacion.FechaCierre,
            MotivoCierre = donacion.MotivoCierre,
            StockResultante = stock
        };

    private async Task NotificarStockAsync(CancellationToken cancellationToken)
    {
        var actualizacion = new StockActualizadoDto
        {
            InsumoId = inventarioResultado.InsumoId,
            Nombre = inventarioResultado.InsumoNombre,
            UnidadMedida = inventarioResultado.UnidadMedida,
            StockActual = inventarioResultado.StockActual,
            StockPrevio = inventarioResultado.StockPrevio,
            MovimientoId = inventarioResultado.MovimientoId,
            TipoMovimiento = inventarioResultado.TipoMovimiento,
            Cantidad = inventarioResultado.Cantidad,
            FechaHora = inventarioResultado.FechaHora
        };

        try
        {
            await notifier.NotificarStockActualizadoAsync(actualizacion, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Stock persistido pero no se pudo notificar la actualizacion del insumo {Insumo}",
                inventarioResultado.InsumoNombre);
        }
    }

    private async Task InvalidarCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cacheService.RemoveByPrefixAsync(CacheDonaciones, cancellationToken);
            await cacheService.RemoveByPrefixAsync(CacheKardex, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo invalidar la cache de donaciones/kardex");
        }
    }

    private static string FormatearErrores(ValidationResult validacion) =>
        string.Join(" ", validacion.Errors.Select(error => error.ErrorMessage));

    private static string GenerarCodigoSeguimiento()
    {
        var numero = Random.Shared.Next(1, 100_000);
        return $"{PrefijoCodigo}-{DateTimeOffset.UtcNow.Year}-{numero:D5}";
    }

    private static bool EsViolacionDeUnico(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
