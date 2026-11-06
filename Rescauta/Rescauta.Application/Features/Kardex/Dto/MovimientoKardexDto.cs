using Rescauta.Domain.Entities.Kardex;

namespace Rescauta.Application.Features.Kardex.Dto;

/// <summary>
/// Datos de entrada para asentar un movimiento en el kardex. Es lo que llega del controller
/// (capa API), no la entidad: el endpoint no debe poder construir un MovimientoKardex
/// saltandose las invariantes del agregado.
///
/// Se validan con un FluentValidation (Paso 3 de la Fase 2), no aqui. Los records con
/// init se mapean al dominio dentro del servicio.
/// </summary>
public sealed record MovimientoKardexDto
{
    /// <summary>Insumo que se mueve. Obligatorio.</summary>
    public Guid InsumoId { get; init; }

    /// <summary>Entrada o salida. Obligatorio.</summary>
    public TipoMovimiento TipoMovimiento { get; init; }

    /// <summary>
    /// Cantidad en la unidad de medida del insumo. Decimal, nunca int: el almacen maneja
    /// kilos y litros con decimales. El validador exige que sea mayor que cero.
    /// </summary>
    public decimal Cantidad { get; init; }

    /// <summary>Quien ejecuta. Obligatorio, es el nombre de la persona.</summary>
    public string Responsable { get; init; } = string.Empty;

    /// <summary>Motivo del movimiento, por ejemplo "Compra a Central de Abastos".</summary>
    public string Concepto { get; init; } = string.Empty;

    /// <summary>
    /// Momento del movimiento. Si viene null, el servicio usa la hora actual en UTC.
    /// Existe para cargas historicas o importes fuera de linea: lo normal son movimientos
    /// en vivo, y entonces llega null.
    /// </summary>
    public DateTimeOffset? FechaHora { get; init; }
}

/// <summary>
/// Resultado de asentar un movimiento. Es lo que devuelve <c>RegistrarMovimientoAsync</c>.
/// </summary>
public sealed record MovimientoKardexResultDto
{
    /// <summary>El asiento creado, ya con Id asignado.</summary>
    public Guid MovimientoId { get; init; }

    /// <summary>Insumo afectado.</summary>
    public Guid InsumoId { get; init; }

    /// <summary>Saldo del insumo despues de aplicar el movimiento.</summary>
    public decimal StockActual { get; init; }

    /// <summary>Unidad de medida del insumo, para que el cliente no tenga que consultarla.</summary>
    public string UnidadMedida { get; init; } = string.Empty;

    /// <summary>Momento del asiento.</summary>
    public DateTimeOffset FechaHora { get; init; }

    /// <summary>Sentido del asiento.</summary>
    public TipoMovimiento TipoMovimiento { get; init; }
}

/// <summary>
/// Payload de <c>kardex.stock.actualizado</c>. Solo ids y primitivos: cruza la frontera
/// hacia SignalR, asi que nada de entidades de EF.
/// </summary>
public sealed record StockActualizadoDto
{
    public Guid InsumoId { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string UnidadMedida { get; init; } = string.Empty;

    /// <summary>Saldo resultante.</summary>
    public decimal StockActual { get; init; }

    /// <summary>Saldo anterior, para que el cliente pueda hacer diff sin pedirlo extra.</summary>
    public decimal StockPrevio { get; init; }

    public Guid MovimientoId { get; init; }
    public TipoMovimiento TipoMovimiento { get; init; }
    public decimal Cantidad { get; init; }
    public DateTimeOffset FechaHora { get; init; }
}

/// <summary>
/// Payload de <c>kardex.alerta.emergencia</c>.
/// </summary>
public sealed record AlertaEmergenciaDto
{
    public Guid? InsumoId { get; init; }
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Stock actual, o null si la alerta no viene de un insumo concreto.</summary>
    public decimal? StockActual { get; init; }

    /// <summary>Descripcion legible para el operador. Sin acentos ni enye, por convencion.</summary>
    public string Mensaje { get; init; } = string.Empty;

    /// <summary>
    /// Niveles: "critico", "urgente", "aviso". Texto y no enum porque este valor viaja al
    /// cliente, que lo pinta tal cual y no va a compilar contra el servidor.
    /// </summary>
    public string Nivel { get; init; } = "critico";

    public DateTimeOffset FechaHora { get; init; } = DateTimeOffset.UtcNow;
}
