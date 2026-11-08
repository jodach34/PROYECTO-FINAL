using System.Text.Json.Serialization;

namespace PROYECTO_FINAL.Models.Api;

/// <summary>
/// Forma de la respuesta que devuelve la API, tal cual la emite. Son tipos de SOLO LECTURA
/// porque el cliente nunca construye uno: si algo esta mal, lo dice la API, no el Razor.
/// Todas las propiedades llegan en camelCase porque la API serializa con esa politica.
/// </summary>
public sealed class ComedorResumen
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
    public int RacionesDiarias { get; set; }

    /// <summary>Posicion horizontal del pin en el mapa, de 0 a 100.</summary>
    public decimal MapaX { get; set; }

    /// <summary>Posicion vertical del pin en el mapa, de 0 a 100.</summary>
    public decimal MapaY { get; set; }

    /// <summary>Latitud GPS real del comedor (WGS84).</summary>
    public decimal Latitud { get; set; }

    /// <summary>Longitud GPS real del comedor (WGS84).</summary>
    public decimal Longitud { get; set; }

    public decimal PorcentajeAbastecimiento { get; set; }

    /// <summary>Abastecido, Alerta, Emergencia o SinDatos. Ya viene como texto de la API.</summary>
    public string Estado { get; set; } = string.Empty;

    public int TotalInsumos { get; set; }
}

public sealed class ComedorDetalle
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string ContactoTelefono { get; set; } = string.Empty;
    public int RacionesDiarias { get; set; }
    public decimal MapaX { get; set; }
    public decimal MapaY { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public decimal PorcentajeAbastecimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public List<InsumoInventario> Inventario { get; set; } = [];
}

public sealed class InsumoInventario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockActual { get; set; }
    public decimal StockMaximo { get; set; }

    /// <summary>Null cuando el insumo no tiene tope conocido. La vista lo muestra como guion.</summary>
    public decimal? Porcentaje { get; set; }

    public decimal? DiasCobertura { get; set; }
}

public sealed class CrearInsumoRequest
{
    public Guid ComedorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockInicial { get; set; }
    public decimal StockMaximo { get; set; }
}

public sealed class InsumoRegistrado
{
    public Guid InsumoId { get; set; }
    public Guid ComedorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal StockActual { get; set; }
    public decimal StockMaximo { get; set; }
}

public sealed class MovimientoKardexRequest
{
    public Guid InsumoId { get; set; }
    public int TipoMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public string Responsable { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public DateTimeOffset? FechaHora { get; set; }
}

public sealed class MovimientoKardexResult
{
    public Guid MovimientoId { get; set; }
    public Guid InsumoId { get; set; }
    public decimal StockActual { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public int TipoMovimiento { get; set; }
}

public sealed class KardexFila
{
    public Guid MovimientoId { get; set; }
    public Guid InsumoId { get; set; }
    public string InsumoNombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>"Entrada" o "Salida", ya como texto.</summary>
    public string TipoMovimiento { get; set; } = string.Empty;

    public decimal Cantidad { get; set; }

    /// <summary>Con signo: positivo suma, negativo resta.</summary>
    public decimal Delta { get; set; }

    public string Responsable { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
}

public sealed class DonacionRegistrada
{
    public Guid DonacionId { get; set; }
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public Guid InsumoId { get; set; }
    public string InsumoNombre { get; set; } = string.Empty;
    public Guid ComedorId { get; set; }
    public string ComedorNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public string Donante { get; set; } = string.Empty;
    public string? PuntoRecojo { get; set; }

    /// <summary>Latitud GPS del punto de recogida. Null si no se registro.</summary>
    public decimal? LatitudOrigen { get; set; }

    /// <summary>Longitud GPS del punto de recogida. Null si no se registro.</summary>
    public decimal? LongitudOrigen { get; set; }

    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset FechaDonacion { get; set; }
    public DateTimeOffset? FechaConfirmacion { get; set; }
    public DateTimeOffset? FechaEnTransito { get; set; }
    public DateTimeOffset? FechaEntrega { get; set; }
    public DateTimeOffset? FechaCierre { get; set; }
    public string? MotivoCierre { get; set; }
    public decimal StockResultante { get; set; }
}

/// <summary>Respuesta reducida de la consulta publica de seguimiento.</summary>
public sealed class SeguimientoDonacionPublico
{
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string InsumoNombre { get; set; } = string.Empty;
    public string ComedorNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset FechaDonacion { get; set; }
}

public sealed class DonacionHistorialApi
{
    public string EstadoAnterior { get; set; } = string.Empty;
    public string EstadoNuevo { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset Fecha { get; set; }
}

public sealed class MotivoDonacionRequest
{
    public string Motivo { get; set; } = string.Empty;
}

public sealed class DonacionFiltroRequest
{
    public string? Estado { get; set; }
    public Guid? ComedorId { get; set; }
    public Guid? InsumoId { get; set; }
    public string? Busqueda { get; set; }
    public DateTimeOffset? Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 20;
}

public sealed class PagedResultApi<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}

/// <summary>Cuerpo del POST de donacion. Esta vez si es de escritura.</summary>
public sealed class DonacionRequest
{
    public Guid InsumoId { get; set; }
    public int Cantidad { get; set; }
    public string Donante { get; set; } = string.Empty;
    public string PuntoRecojo { get; set; } = string.Empty;

    /// <summary>Latitud GPS del punto de recogida. Opcional.</summary>
    public decimal? LatitudOrigen { get; set; }

    /// <summary>Longitud GPS del punto de recogida. Opcional.</summary>
    public decimal? LongitudOrigen { get; set; }
}

/// <summary>
/// Cuerpo de error que devuelve la API cuando algo se rechaza. La API responde con
/// ProblemDetails y mete el motivo en "detail" y el codigo en "title".
/// </summary>
public sealed class ApiError
{
    [JsonPropertyName("title")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detalle { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}
