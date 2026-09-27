using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Donaciones.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Donaciones;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Application.Features.Donaciones;

/// <summary>
/// Casos de uso de donacion.
///
/// REGLA CENTRAL DE RegistrarAsync: el saldo NUNCA se toca a mano. La donacion no suma
/// stock por su cuenta; llama a <see cref="Insumo.RegistrarEntrada"/>, que es el metodo de
/// negocio del agregado y el unico que puede mover <c>StockActual</c>. Si este servicio
/// hiciera <c>insumo.StockActual += cantidad</c>, el inventario podria descuadrarse del
/// kardex sin que nadie lo note.
///
/// La transaccion es una sola: si el asiento se asienta y la donacion no se guarda, queda
/// mercancia entrando sin donante, que es exactamente el escenario que el kardex existe
/// para evitar.
/// </summary>
public sealed class DonacionService(
    IAppDbContext dbContext,
    ILogger<DonacionService> logger) : IDonacionService
{
    /// <summary>
    /// Prefijo del codigo de seguimiento. El anio va dentro porque el volumen de donaciones
    /// de una organizacion pequena cabe de sobra en 5 digitos y asi el codigo se lee solo
    /// por telefono.
    /// </summary>
    private const string PrefijoCodigo = "DON";

    public async Task<Result<DonacionResultDto>> RegistrarAsync(
        RegistrarDonacionDto donacion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(donacion);

        if (donacion.InsumoId == Guid.Empty)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                "El insumo a donar es obligatorio.");
        }

        if (donacion.Cantidad <= 0)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                "La cantidad debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(donacion.Donante))
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.Validacion,
                "El nombre del donante es obligatorio.");
        }

        // AsNoTracking NO: el agregado tiene que quedar tracked para que EF vea el cambio
        // de StockActual y el movimiento hijo agregado en la coleccion.
        var insumo = await dbContext
            .Set<Insumo>()
            .FirstOrDefaultAsync(x => x.Id == donacion.InsumoId, cancellationToken);

        if (insumo is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.InsumoNoEncontrado,
                $"No existe un insumo con id {donacion.InsumoId}.");
        }

        // El destino se resuelve del insumo, nunca del cliente: es la unica forma de que el
        // codigo de donacion apunte a algo real, y evita que se done a un comedor que no
        // existe por error de dedo.
        var comedor = await dbContext
            .Set<Comedor>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == insumo.ComedorId, cancellationToken);

        if (comedor is null)
        {
            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.InsumoNoEncontrado,
                $"El insumo {insumo.Nombre} no pertenece a ningun comedor.");
        }

        var concepto = string.IsNullOrWhiteSpace(donacion.PuntoRecojo)
            ? $"Donacion de {donacion.Donante}"
            : $"Donacion de {donacion.Donante} - {donacion.PuntoRecojo}";

        Donacion entidad;
        MovimientoKardex asiento;

        try
        {
            entidad = Donacion.Registrar(
                insumo.Id,
                donacion.Cantidad,
                donacion.Donante,
                GenerarCodigoSeguimiento(),
                fechaDonacion: null);

            // LA REGLA DE NEGOCIO: el saldo lo mueve el agregado, no el servicio. Si el
            // agregado rechaza la entrada (cantidad cero, campos vacios) no se escribe nada.
            asiento = insumo.RegistrarEntrada(
                cantidad: donacion.Cantidad,
                responsable: donacion.Donante,
                concepto: concepto);
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

        // Las dos escrituras se registran a mano. No por descuido, sino porque EF no
        // puede inferirlas solo:
        //
        //   * La donacion es una entidad nueva y aislada: no cuelga de ninguna navegacion,
        //     asi que no hay de donde descubrirla. Sin este Add no se guardaria nunca, y el
        //     POST devolveria 201 con una donacion que no existe en la base.
        //   * El asiento es hijo del agregado Insumo, y la coleccion del dominio es de solo
        //     lectura, asi que EF lo toma por una entidad ya existente, lo manda como UPDATE
        //     contra una fila que no esta, y el UPDATE afecta cero filas.
        //
        // El saldo no necesita el mismo trato: es una propiedad escalar y EF la ve sin
        // problema, porque el agregado se cargo con una consulta.
        dbContext.Set<Donacion>().Add(entidad);
        dbContext.Set<MovimientoKardex>().Add(asiento);

        try
        {
            // Un solo SaveChanges: la donacion y el asiento se escriben juntos o ninguno.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnico(ex))
        {
            // Lo unico que puede chocar aqui es el indice unico del codigo de seguimiento.
            // El agregado no lanza por eso en memoria, asi que la garantia real la da la
            // base: aqui solo se traduce el fallo a un mensaje que el cliente pueda
            // reintentar sin sacar al operador de la pantalla.
            logger.LogWarning(ex, "Colision del codigo de seguimiento de la donacion.");

            return Result<DonacionResultDto>.Failure(
                DonacionErrorCodes.ErrorInterno,
                "No se pudo generar un codigo de seguimiento unico. Reintente.");
        }

        var resultado = new DonacionResultDto
        {
            DonacionId = entidad.Id,
            CodigoSeguimiento = entidad.CodigoSeguimiento,
            InsumoId = insumo.Id,
            InsumoNombre = insumo.Nombre,
            ComedorId = comedor.Id,
            ComedorNombre = comedor.Nombre,
            Cantidad = entidad.Cantidad,
            UnidadMedida = insumo.UnidadMedida,
            Donante = entidad.Donante,
            Estado = entidad.Estado.ToString(),
            FechaDonacion = entidad.FechaDonacion,
            StockResultante = insumo.StockActual
        };

        logger.LogInformation(
            "Donacion registrada. Codigo={Codigo}, Comedor={Comedor}, Insumo={Insumo}, Cantidad={Cantidad}, StockResultante={Stock}",
            entidad.CodigoSeguimiento,
            comedor.Nombre,
            insumo.Nombre,
            entidad.Cantidad,
            insumo.StockActual);

        return Result<DonacionResultDto>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<DonacionResultDto>>> ObtenerAsync(
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        if (limite is < 1 or > 200)
        {
            return Result<IReadOnlyList<DonacionResultDto>>.Failure(
                DonacionErrorCodes.Validacion,
                "El limite debe estar entre 1 y 200.");
        }

        // Una sola consulta con dos subconsultas correlacionadas en vez de tres
        // round-trips: el nombre del insumo y el del comedor son los unicos datos que
        // Donacion no trae (solo guarda InsumoId).
        //
        // EL ORDEN Y EL ESTADO SE RESUELVEN EN MEMORIA por dos razones distintas, ambas por
        // el proveedor:
        //
        //   * FechaDonacion es DateTimeOffset y SQLite no admite ORDER BY sobre ese tipo
        //     ("SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY
        //     clauses"). Por eso no hay orderby aqui: el limite se aplica despues de
        //     ordenar, o se trae una lista sin los N mas recientes.
        //   * Estado.ToString() tampoco se traduce a SQL. El enum vuelve como numero y se
        //     convierte aqui, que es donde vive la regla de presentacion. Por eso el DTO lo
        //     expone como texto y el Razor no tiene que conocer el enum.
        var filas = await (
            from d in dbContext.Set<Donacion>().AsNoTracking()
            join i in dbContext.Set<Insumo>().AsNoTracking() on d.InsumoId equals i.Id into insumos
            from i in insumos.DefaultIfEmpty()
            join c in dbContext.Set<Comedor>().AsNoTracking() on i.ComedorId equals c.Id into comedores
            from c in comedores.DefaultIfEmpty()
            select new
            {
                d.Id,
                d.CodigoSeguimiento,
                d.InsumoId,
                InsumoNombre = i != null ? i.Nombre : string.Empty,
                UnidadMedida = i != null ? i.UnidadMedida : string.Empty,
                ComedorId = i != null ? i.ComedorId : Guid.Empty,
                ComedorNombre = c != null ? c.Nombre : string.Empty,
                d.Cantidad,
                d.Donante,
                d.Estado,
                d.FechaDonacion,
                d.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var lista = filas
            .OrderByDescending(d => d.FechaDonacion)
            .ThenByDescending(d => d.CreatedAt)
            .Take(limite)
            .Select(d => new DonacionResultDto
            {
                DonacionId = d.Id,
                CodigoSeguimiento = d.CodigoSeguimiento,
                InsumoId = d.InsumoId,
                InsumoNombre = d.InsumoNombre,
                UnidadMedida = d.UnidadMedida,
                ComedorId = d.ComedorId,
                ComedorNombre = d.ComedorNombre,
                Cantidad = d.Cantidad,
                Donante = d.Donante,
                Estado = d.Estado.ToString(),
                FechaDonacion = d.FechaDonacion
            })
            .ToList();

        return Result<IReadOnlyList<DonacionResultDto>>.Success(lista);
    }

    /// <summary>
    /// Genera el codigo de seguimiento: DON-2026-00421.
    ///
    /// La parte aleatoria usa <see cref="Random.Shared"/> y no un Guid porque el codigo lo
    /// lee una persona por telefono y tiene que ser corto. El indice unico de la tabla es
    /// la garantia real de que no se repita; esto solo hace la colision improbable.
    /// </summary>
    private static string GenerarCodigoSeguimiento()
    {
        var numero = Random.Shared.Next(1, 100_000);
        return $"{PrefijoCodigo}-{DateTimeOffset.UtcNow.Year}-{numero:D5}";
    }

    /// <summary>
    /// Distingue la violacion del indice unico de una falla de base de datos cualquiera.
    /// SQLite reporta la restriccion como "UNIQUE constraint failed".
    /// </summary>
    private static bool EsViolacionDeUnico(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
