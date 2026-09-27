namespace Rescauta.Application.Features.Comedores.Dto;

/// <summary>
/// Codigos de error del modulo Comedores. Texto estable, no mensaje: el cliente decide que
/// pintar comparando el codigo, no leyendo el texto (que puede cambiar).
/// </summary>
public static class ComedorErrorCodes
{
    public const string ComedorNoEncontrado = "comedor.no_encontrado";
    public const string Validacion = "comedor.validacion";
    public const string ErrorInterno = "comedor.error_interno";
}

/// <summary>
/// Niveles de abastecimiento de un comedor, en el orden en que el mapa los ordena de mas
/// urgente a menos.
///
/// Se devuelve como TEXTO y no como enum a proposito: estos DTOs cruzan al cliente Razor,
/// que no compila contra el servidor y pinta el valor tal cual. Un "Emergencia" recibido como
/// 0 obliga al cliente a tener la tabla de conversion duplicada.
/// </summary>
public static class EstadoComedor
{
    /// <summary>Despensa por encima del 60% de su capacidad.</summary>
    public const string Abastecido = "Abastecido";

    /// <summary>Entre 30% y 60%: aguanta, pero hay que reponer.</summary>
    public const string Alerta = "Alerta";

    /// <summary>Por debajo del 30%: no llega al siguiente servicio.</summary>
    public const string Emergencia = "Emergencia";

    /// <summary>Sin insumos registrados: no se sabe nada de el.</summary>
    public const string SinDatos = "Sin datos";

    /// <summary>
    /// Traduce el porcentaje de abastecimiento a su nivel. El corte es el mismo que usa la
    /// tabla de recomendacion del Banco de Alimentos, para que un comedor no pueda salir
    /// "Abastecido" en el mapa y "Emergencia" como recomendacion.
    /// </summary>
    public static string DesdePorcentaje(decimal porcentaje) => porcentaje switch
    {
        >= 60m => Abastecido,
        >= 30m => Alerta,
        _ => Emergencia
    };

    /// <summary>Prioridad numérica: mas alto es mas urgente. La usa el orden del mapa.</summary>
    public static int Prioridad(string estado) => estado switch
    {
        Emergencia => 0,
        Alerta => 1,
        Abastecido => 2,
        _ => 3
    };
}

/// <summary>
/// Un comedor en la lista del mapa y del sidebar. Es la proyeccion mas ligera: el mapa
/// pinta 5 de estos y no necesita el inventario completo de cada uno.
/// </summary>
public sealed record ComedorResumenDto
{
    public Guid Id { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string Distrito { get; init; } = string.Empty;

    public int RacionesDiarias { get; init; }

    /// <summary>Posicion del pin en el mapa, en porcentaje (0-100). Ver Comedor.MapaX.</summary>
    public decimal MapaX { get; init; }

    public decimal MapaY { get; init; }

    /// <summary>Promedio de abastecimiento de todos sus insumos, 0-100.</summary>
    public decimal PorcentajeAbastecimiento { get; init; }

    /// <summary>Ver <see cref="EstadoComedor"/>.</summary>
    public string Estado { get; init; } = EstadoComedor.SinDatos;

    public int TotalInsumos { get; init; }
}

/// <summary>
/// Ficha completa de un comedor: lo que pinta la tarjeta flotante del mapa.
/// </summary>
public sealed record ComedorDetalleDto
{
    public Guid Id { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string Distrito { get; init; } = string.Empty;

    public string Direccion { get; init; } = string.Empty;

    public string ContactoTelefono { get; init; } = string.Empty;

    public int RacionesDiarias { get; init; }

    public decimal MapaX { get; init; }

    public decimal MapaY { get; init; }

    public decimal PorcentajeAbastecimiento { get; init; }

    public string Estado { get; init; } = EstadoComedor.SinDatos;

    /// <summary>Despensa insumo por insumo, con su porcentaje.</summary>
    public IReadOnlyList<InsumoInventarioDto> Inventario { get; init; } = [];
}

/// <summary>Un insumo dentro de la despensa de un comedor.</summary>
public sealed record InsumoInventarioDto
{
    public Guid Id { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string UnidadMedida { get; init; } = string.Empty;

    public decimal StockActual { get; init; }

    public decimal StockMaximo { get; init; }

    /// <summary>0-100. Es null cuando el insumo no tiene tope configurado.</summary>
    public decimal? Porcentaje { get; init; }

    /// <summary>
    /// Dias de autonomy que cubre el stock actual, a la consumo del comedor. Null si no
    /// hay tope con el que comparar. Es el numero que la tarjeta de gas muestra ("4 dias").
    /// </summary>
    public decimal? DiasCobertura { get; init; }
}

/// <summary>
/// Fila de la tabla de kardex del Panel de Control. A diferencia del
/// <c>MovimientoKardexResultDto</c> del modulo Kardex (que existe para confirmar una
/// escritura y por eso no lleva responsable ni concepto), este trae lo que la tabla muestra.
/// </summary>
public sealed record KardexFilaDto
{
    public Guid MovimientoId { get; init; }

    public Guid InsumoId { get; init; }

    public string InsumoNombre { get; init; } = string.Empty;

    public string UnidadMedida { get; init; } = string.Empty;

    /// <summary>"Entrada" o "Salida", ya en texto.</summary>
    public string TipoMovimiento { get; init; } = string.Empty;

    public decimal Cantidad { get; init; }

    /// <summary>Cantidad con signo: +12, -4.5. Es lo que la tabla pinta en verde o rojo.</summary>
    public decimal Delta { get; init; }

    public string Responsable { get; init; } = string.Empty;

    public string Concepto { get; init; } = string.Empty;

    public DateTimeOffset FechaHora { get; init; }
}
