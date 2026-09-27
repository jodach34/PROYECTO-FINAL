using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Comedores.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Application.Features.Comedores;

/// <summary>
/// Implementacion de los casos de uso de lectura del mapa.
///
/// Dependencia: <see cref="IAppDbContext"/>, no AppDbContext. La capa Application no
/// referencia Infrastructure (ver Rescauta/README.md).
///
/// Todo el calculo de negocio vive aqui y no en el controller: el "porcentaje de
/// abastecimiento" y el "estado" que ordena el mapa se derivan del kardex, asi que si los
/// calculara la capa API cada pantalla los calcularia distinto.
/// </summary>
public sealed class ComedorService(IAppDbContext dbContext) : IComedorService
{
    /// <summary>
    /// Corte de la franja de "Abastecido". Ver EstadoComedor.DesdePorcentaje.
    /// </summary>
    private const decimal PorcentajeAbastecido = 60m;

    public async Task<Result<IReadOnlyList<ComedorResumenDto>>> ObtenerResumenAsync(
        CancellationToken cancellationToken = default)
    {
        // Proyeccion directa a DTO: se piden 6 columnas y no las entidades con sus
        // colecciones. En el mapa se pintan 5 filas, pero la consulta es la misma que
        // haria la vista de detalle y no vale la pena tener dos.
        //
        // Los totales NO se calculan en SQL a proposito. SQLite no puede aplicar SUM sobre
        // decimal ("SQLite cannot apply aggregate operator 'Sum' on expressions of type
        // 'decimal'"), que es el tipo de StockActual y StockMaximo. Traer los dos numeros
        // por insumo y sumarlos aqui cuesta unas decenas de filas y es la unica forma que
        // funciona en el proveedor que el proyecto usa de verdad. Con SQL Server el
        // HAVING/SUM habria valido, pero el proyecto no usa SQL Server.
        var filas = await dbContext
            .Set<Comedor>()
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Distrito,
                c.RacionesDiarias,
                c.MapaX,
                c.MapaY,
                Suministros = c.Suministros
                    .Select(i => new { i.StockActual, i.StockMaximo })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var resumen = filas
            .Select(f =>
            {
                var porcentaje = CalcularPorcentaje(
                    f.Suministros.Sum(s => s.StockActual),
                    f.Suministros.Sum(s => s.StockMaximo));

                return new ComedorResumenDto
                {
                    Id = f.Id,
                    Nombre = f.Nombre,
                    Distrito = f.Distrito,
                    RacionesDiarias = f.RacionesDiarias,
                    MapaX = f.MapaX,
                    MapaY = f.MapaY,
                    PorcentajeAbastecimiento = porcentaje,
                    Estado = f.Suministros.Count == 0
                        ? EstadoComedor.SinDatos
                        : EstadoComedor.DesdePorcentaje(porcentaje),
                    TotalInsumos = f.Suministros.Count
                };
            })
            // Orden por urgencia, no por nombre: el mapa existe para que se vea primero lo
            // que se esta quedando sin comida. A igual urgencia, por nombre, para que la
            // lista no baile entre recargas.
            .OrderBy(c => EstadoComedor.Prioridad(c.Estado))
            .ThenBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<ComedorResumenDto>>.Success(resumen);
    }

    public async Task<Result<ComedorDetalleDto>> ObtenerDetalleAsync(
        Guid comedorId,
        CancellationToken cancellationToken = default)
    {
        if (comedorId == Guid.Empty)
        {
            return Result<ComedorDetalleDto>.Failure(
                ComedorErrorCodes.Validacion,
                "El id del comedor es obligatorio.");
        }

        var detalle = await dbContext
            .Set<Comedor>()
            .AsNoTracking()
            .Where(c => c.Id == comedorId)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Distrito,
                c.Direccion,
                c.ContactoTelefono,
                c.RacionesDiarias,
                c.MapaX,
                c.MapaY,
                Insumos = c.Suministros
                    .Select(i => new
                    {
                        i.Id,
                        i.Nombre,
                        i.UnidadMedida,
                        i.StockActual,
                        i.StockMaximo
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detalle is null)
        {
            return Result<ComedorDetalleDto>.Failure(
                ComedorErrorCodes.ComedorNoEncontrado,
                $"No existe un comedor con id {comedorId}.");
        }

        var inventario = detalle.Insumos
            .Select(i =>
            {
                var porcentaje = i.StockMaximo > 0m
                    ? Math.Round(i.StockActual / i.StockMaximo * 100m, 1)
                    : (decimal?)null;

                return new InsumoInventarioDto
                {
                    Id = i.Id,
                    Nombre = i.Nombre,
                    UnidadMedida = i.UnidadMedida,
                    StockActual = i.StockActual,
                    StockMaximo = i.StockMaximo,
                    Porcentaje = porcentaje,

                    // DiasCobertura se queda en null a proposito. Serian
                    // stock / consumo_diario, y el consumo diario no existe todavia en el
                    // dominio: no hay de donde sacarlo. Se devuelve null en vez de un
                    // numero inventado, que es peor porque el operador lo toma en serio.
                    DiasCobertura = null
                };
            })
            .OrderBy(i => i.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sumaStock = inventario.Sum(i => i.StockActual);
        var sumaTope = inventario.Sum(i => i.StockMaximo);
        var porcentaje = CalcularPorcentaje(sumaStock, sumaTope);

        var resultado = new ComedorDetalleDto
        {
            Id = detalle.Id,
            Nombre = detalle.Nombre,
            Distrito = detalle.Distrito,
            Direccion = detalle.Direccion,
            ContactoTelefono = detalle.ContactoTelefono,
            RacionesDiarias = detalle.RacionesDiarias,
            MapaX = detalle.MapaX,
            MapaY = detalle.MapaY,
            PorcentajeAbastecimiento = porcentaje,
            Estado = inventario.Count == 0
                ? EstadoComedor.SinDatos
                : EstadoComedor.DesdePorcentaje(porcentaje),
            Inventario = inventario
        };

        return Result<ComedorDetalleDto>.Success(resultado);
    }

    public async Task<Result<IReadOnlyList<KardexFilaDto>>> ObtenerKardexAsync(
        Guid comedorId,
        int limite = 20,
        CancellationToken cancellationToken = default)
    {
        if (comedorId == Guid.Empty)
        {
            return Result<IReadOnlyList<KardexFilaDto>>.Failure(
                ComedorErrorCodes.Validacion,
                "El id del comedor es obligatorio.");
        }

        if (limite is < 1 or > 200)
        {
            return Result<IReadOnlyList<KardexFilaDto>>.Failure(
                ComedorErrorCodes.Validacion,
                "El limite debe estar entre 1 y 200.");
        }

        // Un solo JOIN con subconsulta correlacionada. La alternativa (sacar los ids de los
        // insumos, luego un Dictionary de nombres, luego los movimientos) son tres
        // round-trips y mas memoria para pintar una tabla de 20 filas.
        //
        // El JOIN es por ComedorId y no por la coleccion de movimientos de Comedor a
        // proposito: el kardex se mueve solo desde el agregado Insumo, y que el mapa pueda
        // recorrerlo no significa que pueda escribirlo.
        //
        // EL ORDEN SE HACE EN MEMORIA, no en SQL, y no por gusto: SQLite no admite
        // ORDER BY sobre DateTimeOffset ("SQLite does not support expressions of type
        // 'DateTimeOffset' in ORDER BY clauses"), que es el tipo de FechaHora. La fecha se
        // ordena porque en memoria; el limite se aplica DESPUES de ordenar, porque si se
        // pidiera Take antes de ordernar traeria 20 asientos sin criterio y no los 20 mas
        // recientes. El costo es traer los asientos del comedor antes de recortarlos, que
        // para un kardex de un solo comedor es un numero acotado; si algun dia un comedor
        // llegara a cientos de miles de asientos habria que traerlos paginados por id.
        var filas = await (
            from i in dbContext.Set<Insumo>().AsNoTracking()
            join m in dbContext.Set<MovimientoKardex>().AsNoTracking() on i.Id equals m.InsumoId
            where i.ComedorId == comedorId
            select new
            {
                m.Id,
                m.InsumoId,
                m.TipoMovimiento,
                m.Cantidad,
                m.Responsable,
                m.Concepto,
                m.FechaHora,
                m.CreatedAt,
                i.Nombre,
                i.UnidadMedida
            })
            .ToListAsync(cancellationToken);

        var kardex = filas
            // El desempate por CreatedAt replica el criterio que ya usaba la version con SQL:
            // dos asientos con la misma FechaHora (un lote cargado de golpe) se ordenan por
            // el orden en que se registraron, no en uno arbitrario.
            .OrderByDescending(m => m.FechaHora)
            .ThenByDescending(m => m.CreatedAt)
            .Take(limite)
            .Select(m => new KardexFilaDto
            {
                MovimientoId = m.Id,
                InsumoId = m.InsumoId,
                InsumoNombre = m.Nombre,

                // TipoMovimiento.ToString() NO se puede traducir a SQL, asi que el enum
                // vuelve como numero y se convierte aqui. Por eso el DTO lo expone como
                // texto: la conversion va en la capa Application, no en el Razor.
                TipoMovimiento = m.TipoMovimiento.ToString(),
                UnidadMedida = m.UnidadMedida,
                Cantidad = m.Cantidad,

                // El Delta se calcula aqui y no se persiste: es la misma regla de signo que
                // aplica MovimientoKardex, y duplicarla como columna invita a que se
                // desincronice en una correccion.
                Delta = m.TipoMovimiento == TipoMovimiento.Entrada ? m.Cantidad : -m.Cantidad,
                Responsable = m.Responsable,
                Concepto = m.Concepto,
                FechaHora = m.FechaHora
            })
            .ToList();

        return Result<IReadOnlyList<KardexFilaDto>>.Success(kardex);
    }

    /// <summary>
    /// Porcentaje de abastecimiento ponderado por tope: el total guardado sobre el total
    /// que cabe. NO es el promedio de los porcentajes insumo a insumo, porque ese promedio
    /// da el mismo peso a un balon de gas que a 50 kilos de arroz.
    /// </summary>
    private static decimal CalcularPorcentaje(decimal sumaStock, decimal sumaTope)
    {
        if (sumaTope <= 0m)
        {
            return 0m;
        }

        return Math.Round(Math.Clamp(sumaStock / sumaTope * 100m, 0m, 100m), 1);
    }
}
