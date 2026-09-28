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
/// Estado y Sesion: lo que hay guardado en la cookie, en la sesion del servidor y en
/// TempData, para poder compararlos lado a lado.
///
/// <see cref="Cookies"/> se trae el dictionary crudo de la peticion. Es lo que ve el
/// navegador entero, incluidas las cookies de antiforgery y de sesion: por eso esta pagina
/// sirve tambien para ver, con mis propios ojos, cuales ha ido dejando el framework.
/// </summary>
public sealed record StateDemoViewModel
{
    /// <summary>Contador que vive en la cookie del cliente.</summary>
    public int VisitaActual { get; init; }

    /// <summary>Contador que vive en la sesion del servidor.</summary>
    public int VisitasServidor { get; init; }

    public string Turno { get; init; } = string.Empty;

    public string ComedorActivo { get; init; } = string.Empty;

    public string OperadorGuardado { get; init; } = string.Empty;

    public string UltimaVisita { get; init; } = string.Empty;

    /// <summary>Aviso que venia en TempData desde el request anterior, si habia.</summary>
    public string? Aviso { get; init; }

    public IReadOnlyDictionary<string, string> Cookies { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Un insumo real de la red, para el boton que hace el POST de prueba contra la API.
    /// Es null si la API no responde o si no hay ninguno registrado, y la vista lo oculta:
    /// un boton que va a fallar no debe aparecer.
    /// </summary>
    public Guid? InsumoDemo { get; init; }

    public string? InsumoDemoNombre { get; init; }
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
    /// <summary>Clases de Tailwind para la insignia de estado.</summary>
    public static string ClaseEstado(string estado) => estado switch
    {
        "Abastecido" => "bg-ok/10 text-ok border-ok/25",
        "Alerta" => "bg-alerta/10 text-alerta border-alerta/25",
        "Emergencia" => "bg-critico/10 text-critico border-critico/25",
        _ => "bg-gray-100 text-muted border-gray-200"
    };

    /// <summary>Color solido, sin fondo translucido, para el pin del mapa.</summary>
    public static string ColorEstado(string estado) => estado switch
    {
        "Abastecido" => "ok",
        "Alerta" => "alerta",
        "Emergencia" => "critico",
        _ => "muted"
    };

    /// <summary>Texto del color, para las barras de porcentaje.</summary>
    public static string TextColorEstado(string estado) => estado switch
    {
        "Abastecido" => "text-ok",
        "Alerta" => "text-alerta",
        "Emergencia" => "text-critico",
        _ => "text-muted"
    };
}
