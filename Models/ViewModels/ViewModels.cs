using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Models;

/// <summary>
/// Lo que la pantalla del Mapa necesita pintar. No es la respuesta de la API tal cual: es
/// la respuesta mas lo que el Razor no debe calcular (clases de color, totales).
///
/// <see cref="Error"/> viene a proposito. Si la API no responde, la pantalla se dibuja
/// igual, con la lista vacia y un aviso arriba. Un 500 en blanco no le dice al operador
/// que lo que falta es levantar el otro proceso.
/// </summary>
public sealed record MapaViewModel
{
    public IReadOnlyList<ComedorResumen> Comedores { get; init; } = [];

    /// <summary>
    /// Comedores listos para Leaflet. Se construyen a partir de los mismos datos de la API;
    /// los que no tienen posicion valida no llegan aqui.
    /// </summary>
    public IReadOnlyList<ComedorMapaPunto> PuntosMapa { get; init; } = [];

    public string? Error { get; init; }

    /// <summary>Total de raciones diarias de los comedores registrados.</summary>
    public int TotalRaciones => Comedores.Sum(c => c.RacionesDiarias);

    /// <summary>Cuantos comedores estan en estado de alerta o emergencia.</summary>
    public int EnRiesgo => Comedores.Count(c => c.Estado is "Alerta" or "Emergencia");

    /// <summary>Porcentaje de abastecimiento promedio de la red.</summary>
    public decimal PromedioAbastecimiento => Comedores.Count == 0
        ? 0m
        : Math.Round(Comedores.Average(c => c.PorcentajeAbastecimiento), 1);
}

/// <summary>
/// Un comedor con los datos que necesita un marcador de Leaflet. La direccion viene del
/// endpoint de detalle y la latitud/longitud son la proyeccion de MapaX/MapaY.
/// </summary>
public sealed record ComedorMapaPunto
{
    public Guid Id { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string Direccion { get; init; } = string.Empty;

    public string Distrito { get; init; } = string.Empty;

    public string Estado { get; init; } = string.Empty;

    public double Latitud { get; init; }

    public double Longitud { get; init; }
}

/// <summary>Panel de Control: metricas de toda la red mas el kardex del comedor elegido.</summary>
public sealed record PanelViewModel
{
    public IReadOnlyList<ComedorResumen> Comedores { get; init; } = [];

    /// <summary>Comedor cuyo kardex se esta mostrando. Puede ser null si no hay ninguno.</summary>
    public ComedorDetalle? Comedor { get; init; }

    public IReadOnlyList<KardexFila> Kardex { get; init; } = [];

    public string? Error { get; init; }

    /// <summary>Verdadero cuando la API respondio pero se pidio un comedor que no existe.</summary>
    public bool ComedorNoEncontrado { get; init; }

    public int TotalComedores => Comedores.Count;

    public int TotalRaciones => Comedores.Sum(c => c.RacionesDiarias);

    public int ComedoresEnRiesgo => Comedores.Count(c => c.Estado is "Alerta" or "Emergencia");

    public decimal PromedioAbastecimiento => Comedores.Count == 0
        ? 0m
        : Math.Round(Comedores.Average(c => c.PorcentajeAbastecimiento), 1);

    /// <summary>Total de insumos en todas las despensas de los comedores registrados.</summary>
    public int TotalInsumos => Comedores.Sum(c => c.TotalInsumos);
}

public sealed record InventarioViewModel
{
    public IReadOnlyList<ComedorResumen> Comedores { get; init; } = [];

    public ComedorDetalle? Comedor { get; init; }

    public IReadOnlyList<KardexFila> Kardex { get; init; } = [];

    public string? Error { get; init; }

    public string? Exito { get; init; }

    public InsumoFormViewModel Insumo { get; init; } = new();

    public ConsumoFormViewModel Consumo { get; init; } = new();
}

public sealed class InsumoFormViewModel
{
    public Guid? ComedorId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string UnidadMedida { get; set; } = string.Empty;

    public decimal StockInicial { get; set; }

    public decimal StockMaximo { get; set; }
}

public sealed class ConsumoFormViewModel
{
    public Guid? ComedorId { get; set; }

    public Guid? InsumoId { get; set; }

    public decimal Cantidad { get; set; }

    public string Responsable { get; set; } = string.Empty;

    public string Concepto { get; set; } = "Consumo diario";
}

/// <summary>
/// Banco de Alimentos: el formulario de donacion y las opciones para llenarlo.
///
/// Se declara como record y no como class para poder copiarlo con "with" al repintar el
/// formulario tras un error, sin perder lo que el operador ya escribio.
/// </summary>
public sealed record BancoAlimentosViewModel
{
    public IReadOnlyList<ComedorDetalle> Comedores { get; init; } = [];

    public IReadOnlyList<DonacionRegistrada> Donaciones { get; init; } = [];

    public string? Error { get; init; }

    /// <summary>Codigo de seguimiento del ultimo registro exitoso.</summary>
    public string? Exito { get; init; }

    /// <summary>Lo que el operador escribio, para repoblar el formulario tras un error.</summary>
    public string Donante { get; init; } = string.Empty;

    public int Cantidad { get; init; }

    public string? PuntoRecojo { get; init; }

    public Guid? InsumoId { get; init; }

    /// <summary>
    /// Todos los insumos de todos los comedores, para el selector. El value del option es
    /// el id del INsumo porque es lo que la API necesita, no el id del comedor: se dona a
    /// un insumo concreto, no a un comedor entero.
    /// </summary>
    public IEnumerable<(Guid InsumoId, string Texto)> Insumos =>
        Comedores.SelectMany(c => c.Inventario.Select(i => (
            i.Id,
            $"{c.Nombre} - {i.Nombre} ({i.UnidadMedida})")));
}

/// <summary>
/// Cuerpo del formulario de donacion. Es de escritura, a diferencia de los tipos de
/// <c>Models/Api</c>, que son solo lectura.
/// </summary>
public class DonacionViewModel
{
    public Guid? InsumoId { get; set; }

    public int Cantidad { get; set; }

    public string Donante { get; set; } = string.Empty;

    public string? PuntoRecojo { get; set; }
}

/// <summary>
/// Seguimiento publico de una donacion. El donante ingresa el codigo que recibio al
/// registrarla y ve el estado real, el comedor destino y el insumo donado.
///
/// No muestra la lista completa de donaciones: solo la que corresponde al codigo buscado.
/// Ese codigo es el mismo que genera el Banco de Alimentos, asi que las dos pantallas
/// forman un flujo cerrado.
/// </summary>
public sealed record SeguimientoViewModel
{
    public string Codigo { get; init; } = string.Empty;

    public DonacionRegistrada? Donacion { get; init; }

    public string? Error { get; init; }

    /// <summary>Pasos del flujo, para explicar que significa cada estado.</summary>
    public static IReadOnlyList<string> Pasos { get; } =
        ["Pendiente", "Confirmada", "EnTransito", "Entregada"];

    public bool EsEstado(string estado) =>
        string.Equals(Donacion?.Estado, estado, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Listado administrativo de donaciones con filtros y paginacion.</summary>
public sealed record DonacionesGestionViewModel
{
    public IReadOnlyList<DonacionRegistrada> Donaciones { get; init; } = [];

    public IReadOnlyList<ComedorResumen> Comedores { get; init; } = [];

    public string? Estado { get; init; }

    public Guid? ComedorId { get; init; }

    public string? Busqueda { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public bool HasPrevious { get; init; }

    public bool HasNext { get; init; }

    public string? Error { get; init; }

    public string? Exito { get; init; }
}

/// <summary>Detalle de una donacion y su linea de tiempo de estados.</summary>
public sealed record DonacionDetalleViewModel
{
    public DonacionRegistrada? Donacion { get; init; }

    public IReadOnlyList<DonacionHistorialApi> Historial { get; init; } = [];

    public string? Error { get; init; }

    public string? Exito { get; init; }
}

/// <summary>Ficha publica de un comedor, con su inventario actual.</summary>
public sealed record ComedorDetalleViewModel
{
    public ComedorDetalle? Comedor { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Reglas de presentacion compartidas por las tres pantallas.
///
/// Son metodos estaticos y no un servicio: no dependen de nada y convertirlos en injected
/// seria ruido. Viven aqui, y no en el .cshtml, porque el color de "Emergencia" aparece en
/// el mapa, en la lista del panel y en la ficha; si estuviera en la vista, cambiarlo
/// obligaria a editar tres archivos de Razor.
/// </summary>
public static class Presentacion
{
    public static string EtiquetaEstado(string estado) => estado switch
    {
        "Abastecido" => "Abastecido",
        "Alerta" => "Alerta",
        "Emergencia" => "Emergencia",
        _ => "Sin datos"
    };

    /// <summary>Clases del sistema visual comun para la insignia de estado.</summary>
    public static string ClaseEstado(string estado) => $"rs-badge rs-badge--{ColorEstado(estado)}";

    /// <summary>Color semantico reutilizado por pines, barras y puntos.</summary>
    public static string ColorEstado(string estado) => estado switch
    {
        "Abastecido" => "ok",
        "Alerta" => "alerta",
        "Emergencia" => "critico",
        _ => "muted"
    };

    public static string ClaseBarra(string estado) => $"rs-progress__bar rs-progress__bar--{ColorEstado(estado)}";

    public static string ClasePin(string estado) => $"rs-pin rs-pin--{ColorEstado(estado)}";

    public static string EtiquetaEstadoDonacion(string estado) => estado switch
    {
        "Pendiente" => "Pendiente",
        "Confirmada" => "Confirmada",
        "EnTransito" => "En tránsito",
        "Entregada" => "Entregada",
        "Cancelada" => "Cancelada",
        "Anulada" => "Anulada",
        _ => estado
    };

    public static string ClaseEstadoDonacion(string estado) =>
        $"rs-badge rs-badge--{ColorEstadoDonacion(estado)}";

    public static string ColorEstadoDonacion(string estado) => estado switch
    {
        "Pendiente" => "alerta",
        "Confirmada" => "brand",
        "EnTransito" => "brand",
        "Entregada" => "ok",
        "Cancelada" => "muted",
        "Anulada" => "critico",
        _ => "muted"
    };

    public static bool PuedeConfirmarDonacion(string estado) =>
        string.Equals(estado, "Pendiente", StringComparison.OrdinalIgnoreCase);

    public static bool PuedePonerEnTransito(string estado) =>
        string.Equals(estado, "Confirmada", StringComparison.OrdinalIgnoreCase);

    public static bool PuedeEntregarDonacion(string estado) =>
        string.Equals(estado, "EnTransito", StringComparison.OrdinalIgnoreCase);

    public static bool PuedeCerrarDonacion(string estado) =>
        estado is "Pendiente" or "Confirmada" or "EnTransito";

    public static bool PuedeEliminarDonacion(string estado) =>
        estado is "Pendiente" or "Cancelada" or "Anulada";
}
