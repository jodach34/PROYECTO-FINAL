using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos de arranque de la base de desarrollo.
///
/// QUE HACE Y QUE NO HACE: crea los 5 comedores de la maqueta con su despensa y una pila de
/// movimientos, para que el mapa y el panel tengan algo que mostrar en el primer arranque.
/// NO inventa reglas de negocio: los totales salen de <c>Insumo.Crear</c> y los asientos de
/// <c>RegistrarEntrada</c> / <c>RegistrarSalida</c>, igual que en produccion. Por eso los
/// numeros que salen en pantalla cuadran con el kardex, que es lo que se pedig en una demo.
///
/// Es idempotente por <c>AnyAsync</c>: si ya hay comedores no hace nada. Borra el
/// <c>rescauta.dev.db</c> si quieres recargar la semilla.
///
/// Los porcentajes no estan escritos a mano en el seed: se dejan como stock y tope, y el
/// estado (Abastecido / Alerta / Emergencia) lo calcula ComedorService. Si el corte cambiara
/// manana, cambiaria en un solo sitio en vez de en cinco filas de datos.
/// </summary>
public static class DatosSemilla
{
    /// <summary>
    /// Un comedor con su despensa. El record es privado del seed: no es un DTO de la API ni
    /// una entidad, es solo la forma de declarar la tabla de arranque en una linea.
    /// </summary>
    private sealed record ComedorSemilla(
        string Nombre,
        string Distrito,
        int RacionesDiarias,
        string Direccion,
        string Telefono,
        decimal MapaX,
        decimal MapaY,
        (string Nombre, string Unidad, decimal Stock, decimal Tope)[] Insumos);

    /// <summary>
    /// Los 5 comedores de la maqueta, con los mismos nombres, distritos y posiciones de pin
    /// que tenia la version estatica. Cambiar los numeros de stock es lo esperado: son datos.
    /// </summary>
    private static readonly ComedorSemilla[] Comedores =
    [
        new(
            "Olla Común La Esperanza",
            "San Juan de Miraflores",
            180,
            "Asoc. de Vivienda La Esperanza, Sector 3",
            "+51 984 732 104",
            30m,
            41m,
            [
                ("Arroz Extra Fino", "kg", 12m, 80m),
                ("Agua Potable", "litro", 450m, 1000m),
                ("Balón de Gas GLP 10kg", "unidad", 1m, 3m)
            ]),

        new(
            "Comedor Santa Rosa",
            "Villa María del Triunfo",
            150,
            "Av. Los Héroes 1245, VMT",
            "+51 921 554 780",
            49m,
            62m,
            [
                ("Arroz Extra Fino", "kg", 36m, 80m),
                ("Agua Potable", "litro", 900m, 1000m),
                ("Aceite Vegetal", "litro", 12m, 20m)
            ]),

        new(
            "Comedor Las Palmeras",
            "Lurigancho-Chosica",
            220,
            "Av. Túpac Amaru 890, Chosica",
            "+51 933 118 265",
            63m,
            32m,
            [
                ("Arroz Extra Fino", "kg", 66m, 80m),
                ("Agua Potable", "litro", 1000m, 1000m),
                ("Lentejas Partida", "kg", 40m, 50m)
            ]),

        new(
            "Olla Común San Martín",
            "Villa El Salvador",
            95,
            "Av. Rodrigo de Triana 455, VES",
            "+51 945 207 613",
            22m,
            76m,
            [
                ("Arroz Extra Fino", "kg", 30m, 80m),
                ("Agua Potable", "litro", 400m, 1000m),
                ("Balón de Gas GLP 10kg", "unidad", 1m, 2m)
            ]),

        new(
            "Comedor Villa Unión",
            "Pucusana",
            130,
            "Calle Las Begonias 233, Pucusana",
            "+51 958 440 391",
            57m,
            80m,
            [
                ("Arroz Extra Fino", "kg", 59m, 80m),
                ("Agua Potable", "litro", 820m, 1000m),
                ("Aceite Vegetal", "litro", 8m, 20m)
            ])
    ];

    public static async Task SembrarAsync(
        AppDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // AnyAsync y no CountAsync: solo importa si hay algo, no cuantos.
        if (await context.Set<Comedor>().AnyAsync(cancellationToken))
        {
            logger.LogInformation("La semilla se omite: ya hay comedores en la base.");

            return;
        }

        var comedores = new List<Comedor>(Comedores.Length);

        foreach (var fila in Comedores)
        {
            var comedor = Comedor.Crear(
                fila.Nombre,
                fila.Distrito,
                fila.RacionesDiarias,
                fila.Direccion,
                fila.Telefono,
                fila.MapaX,
                fila.MapaY);

            // El comedor se registra en el contexto ANTES que sus insumos. Si solo se
            // anadieran los insumos, EF no tendria al padre de la FK y el INSERT fallaria
            // con "FOREIGN KEY constraint failed" en SQLite.
            context.Set<Comedor>().Add(comedor);

            foreach (var insumo in fila.Insumos)
            {
                // StockInicial = tope: la despensa arranca llena y las SALIDAS de abajo la
                // van vaciando. Asi el estado de cada comedor sale de los asientos y no de
                // un porcentaje escrito a mano.
                context.Set<Insumo>().Add(Insumo.Crear(
                    comedor.Id,
                    insumo.Nombre,
                    insumo.Unidad,
                    stockInicial: insumo.Tope,
                    stockMaximo: insumo.Tope));
            }

            comedores.Add(comedor);
        }

        await context.SaveChangesAsync(cancellationToken);

        // SOLTAR el grafo del change tracker antes de sembrar los movimientos.
        //
        // El motivo es sutil y no es cosmetico. Un Insumo que se creo con Add() y ya se
        // guardo queda tracked pero SIN SNAPSHOT de valores originales: EF solo arma el
        // snapshot cuando la entidad entra por una consulta. Sin snapshot, cuando el
        // movimiento hijo se agrega a la coleccion del agregado, EF no lo reconoce como
        // entidad nueva y lo manda como UPDATE en lugar de INSERT; como la fila todavia no
        // existe, el UPDATE afecta 0 filas y revienta con DbUpdateConcurrencyException.
        //
        // KardexService no sufre esto porque carga el agregado con una consulta. Aqui se
        // replica esa misma situacion: se suelta todo y se vuelve a leer de la base, que es
        // ademas el estado real en el que quedo, no el que nosotros creemos.
        //
        // Los objetos Comedor del listado se conservan solo como PORTADORES de Id y Nombre:
        // son datos planos ya guardados, asi que sobreviven sin tracker.
        context.ChangeTracker.Clear();

        await sembrarMovimientosAsync(context, logger, comedores, cancellationToken);

        logger.LogInformation(
            "Semilla cargada: {Comedores} comedores con su despensa y su kardex inicial.",
            comedores.Count);
    }

    /// <summary>
    /// Asienta unos cuantos movimientos para que la tabla del Panel de Control no arranque
    /// vacia. Se restan del stock inicial, asi que el estado final de cada comedor es el que
    /// resulta de su propio historial y no de un numero arbitrario escrito a mano.
    /// </summary>
    private static async Task sembrarMovimientosAsync(
        AppDbContext context,
        ILogger logger,
        IReadOnlyCollection<Comedor> comedores,
        CancellationToken cancellationToken)
    {
        var hoy = DateTimeOffset.UtcNow;

        // (indice del comedor, filtro de nombre del insumo, tipo, cantidad, responsable,
        //  concepto, dias atras)
        var movimientos = new (int Comedor, string Insumo, TipoMovimiento Tipo, decimal Cantidad,
            string Responsable, string Concepto, int DiasAtras)[]
        {
            (0, "Arroz", TipoMovimiento.Salida, 68m, "Juana Quispe (Cocina)", "Consumo diario", 0),
            (0, "Agua", TipoMovimiento.Salida, 550m, "Juana Quispe (Cocina)", "Consumo diario", 0),
            (0, "Gas", TipoMovimiento.Salida, 2m, "Juana Quispe (Cocina)", "Consumo semanal", 1),

            (1, "Arroz", TipoMovimiento.Salida, 44m, "Rosa Huamán (Cocina)", "Consumo diario", 0),
            (1, "Aceite", TipoMovimiento.Entrada, 12m, "Donación anónima", "Donación", 1),

            (2, "Arroz", TipoMovimiento.Salida, 14m, "Carlos Vega (Cocina)", "Consumo diario", 0),
            (2, "Lentejas", TipoMovimiento.Entrada, 40m, "Dna. Teresa Vargas", "Donación", 2),
            (2, "Agua", TipoMovimiento.Entrada, 1000m, "Cisterna Distrital", "Abastecimiento municipal", 3),

            (3, "Arroz", TipoMovimiento.Salida, 50m, "Luis Alarcón (Cocina)", "Consumo diario", 0),
            (3, "Gas", TipoMovimiento.Salida, 1m, "Luis Alarcón (Cocina)", "Consumo semanal", 4),

            (4, "Arroz", TipoMovimiento.Salida, 21m, "Maria Flores (Cocina)", "Consumo diario", 0),
            (4, "Agua", TipoMovimiento.Salida, 180m, "Maria Flores (Cocina)", "Consumo diario", 0),
            (4, "Aceite", TipoMovimiento.Entrada, 8m, "Panadería San Juan", "Donación", 5)
        };

        var ordenados = comedores.ToList();

        foreach (var movimiento in movimientos)
        {
            var comedor = ordenados[movimiento.Comedor];

            var insumo = await context
                .Set<Insumo>()
                .FirstOrDefaultAsync(
                    i => i.ComedorId == comedor.Id && EF.Functions.Like(i.Nombre, $"%{movimiento.Insumo}%"),
                    cancellationToken);

            if (insumo is null)
            {
                logger.LogWarning(
                    "Semilla: el comedor {Comedor} no tiene el insumo '{Insumo}'. Se omite el movimiento.",
                    comedor.Nombre,
                    movimiento.Insumo);

                continue;
            }

            // La hora del asiento se fija a las 7 de la manana del dia correspondiente, no
            // a medianoche: un consumo registrado a las 00:00 parece hecho de noche, y el
            // panel muestra "Hoy, 07:15 AM".
            var fecha = RestarDias(hoy, movimiento.DiasAtras).AddHours(7);

            // El agregado devuelve el asiento que acaba de crear. Se registra a mano en el
            // contexto en vez de confiar en que EF lo descubra solo en la coleccion.
            //
            // Sin esto, EF no reconoce el asiento como entidad nueva, lo manda como UPDATE
            // contra una fila que todavia no existe, el UPDATE afecta cero filas y
            // SaveChangesAsync revienta con DbUpdateConcurrencyException.
            MovimientoKardex asiento;

            if (movimiento.Tipo == TipoMovimiento.Entrada)
            {
                asiento = insumo.RegistrarEntrada(
                    movimiento.Cantidad,
                    movimiento.Responsable,
                    movimiento.Concepto,
                    fecha);
            }
            else
            {
                // RegistrarSalida lanza si no alcanza. El seed no lo captura a proposito:
                // si una cantidad del seed supera el stock, es un error del seed y debe
                // romper el arranque, no dejar un comedor en negativo sin avisar.
                asiento = insumo.RegistrarSalida(
                    movimiento.Cantidad,
                    movimiento.Responsable,
                    movimiento.Concepto,
                    fecha);
            }

            context.Set<MovimientoKardex>().Add(asiento);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset RestarDias(DateTimeOffset base_, int dias) =>
        base_.AddDays(-dias);
}
