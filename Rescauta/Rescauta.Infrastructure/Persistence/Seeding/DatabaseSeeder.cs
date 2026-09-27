using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Domain.Entities;
using Rescauta.Domain.Enums;
using Rescauta.Domain.ValueObjects;
using Rescauta.Infrastructure.Persistence;

namespace Rescauta.Infrastructure.Persistence.Seeding;

/// <summary>
/// Datos de arranque para desarrollo. SOLO se ejecuta si la base esta vacia, y solo
/// cuando <c>Database:SeedOnStartup</c> esta en true.
///
/// POR QUE EXISTE: sin comedores ni insumos en la base, ningun endpoint de escritura
/// se puede probar. No hay endpoint para crear un comedor o un insumo todavia, asi que
/// un clon nuevo daba una base vacia y los endpoints de kardex, consumo y asignacion
/// respondian 404 o 400 sin que hubiera forma de llegar a ellos.
///
/// Decision de equipo: el seed va ACA y no en un archivo .sql suelto, para que no haya
/// que ejecutarlo a mano. Es idempotente (solo siembra si no hay comedores) y esta
/// protegido por configuracion, asi que en produccion no corre ni por accidente.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await context.Set<Comedor>().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed omitido: la base ya tiene comedores.");

            return;
        }

        logger.LogInformation("Base vacia en desarrollo. Se siembran datos de ejemplo.");

        var comedores = new[]
        {
            new Comedor(
                nombre: "Comedor San Martin de Porres",
                distrito: "Surco",
                direccion: "Av. Primavera 1280, Surco",
                racionesDiarias: 180,
                ubicacion: new UbicacionGeo(-12.1425, -77.0201),
                contactoDirecto: "Juana Quispe"),
            new Comedor(
                nombre: "Comedor Virgen de la Puerta",
                distrito: "San Borja",
                direccion: "Jr. Los Almendros 455, San Borja",
                racionesDiarias: 95,
                ubicacion: new UbicacionGeo(-12.1003, -77.0112),
                contactoDirecto: "Carlos Mendoza"),
            new Comedor(
                nombre: "Comedor San Juan de Dios",
                distrito: "Villa El Salvador",
                direccion: "Av. Los Libertadores 1200, VES",
                racionesDiarias: 240,
                ubicacion: new UbicacionGeo(-12.1975, -76.9442),
                contactoDirecto: "Rosa Huaman")
        };

        await context.Set<Comedor>().AddRangeAsync(comedores, cancellationToken);

        // Insumos de los dos primeros comedores. El tercero se deja sin inventario a
        // proposito: sirve para probar el filtro de "solo comedores criticos", que debe
        // traer a los que tienen stock por debajo del minimo.
        var arrozSanMartin = new Insumo(
            nombre: "Arroz",
            categoria: "Alimentos Secos",
            unidadMedida: "kg",
            comedorId: comedores[0].Id,
            consumoDiarioEstimado: 30m,
            stockMinimo: 25m,
            caducidadAprox: new DateOnly(2027, 6, 30));

        var aceiteSanMartin = new Insumo(
            nombre: "Aceite vegetal",
            categoria: "Alimentos Secos",
            unidadMedida: "L",
            comedorId: comedores[0].Id,
            consumoDiarioEstimado: 8m,
            stockMinimo: 10m,
            caducidadAprox: new DateOnly(2027, 3, 15));

        var aguaVirgen = new Insumo(
            nombre: "Agua potable",
            categoria: "Agua Potable y Liquidos",
            unidadMedida: "L",
            comedorId: comedores[1].Id,
            consumoDiarioEstimado: 50m,
            stockMinimo: 40m,
            caducidadAprox: new DateOnly(2026, 12, 31));

        await context.Set<Insumo>().AddRangeAsync(
            [arrozSanMartin, aceiteSanMartin, aguaVirgen],
            cancellationToken);

        // El stock inicial se registra como una ENTRADA de kardex, no seteando el campo a
        // mano. Asi el historial cuadra con el stock desde el primer momento: si alguien
        // suma los movimientos, obtiene exactamente el StockActual. Sembrar el stock sin
        // movimiento dejaria el kardex descuadrado para siempre.
        foreach (var (insumo, cantidad) in new[]
                 {
                     (arrozSanMartin, 120m),
                     (aceiteSanMartin, 6m),
                     (aguaVirgen, 400m)
                 })
        {
            var stockResultante = insumo.RegistrarEntrada(cantidad);

            await context.Set<MovimientoKardex>().AddAsync(
                new MovimientoKardex(
                    insumo.Id,
                    TipoMovimientoKardex.Entrada,
                    cantidad,
                    stockResultante,
                    DateTimeOffset.UtcNow,
                    referencia: "SEED",
                    responsable: "Datos de ejemplo"),
                cancellationToken);
        }

        // El aceite queda por debajo de su minimo (6 de 10) para que el mapa y el panel
        // tengan un comedor en Alerta desde el primer arranque, sin tener que registrar
        // un consumo a mano.
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seed completado: {Comedores} comedores y {Insumos} insumos con su entrada de kardex.",
            comedores.Length,
            3);
    }
}
