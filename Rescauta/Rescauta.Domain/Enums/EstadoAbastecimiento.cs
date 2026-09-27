namespace Rescauta.Domain.Enums;

/// <summary>
/// Estado de abastecimiento de un comedor. Es lo que la vista de mapa colorea de
/// verde, naranja o rojo, y lo que la API devuelve en <c>EstadoAbastecimiento</c>.
///
/// El orden de los valores es intencional: el numero representa la severidad
/// (1 = abastecido, 3 = emergencia), de modo que los filtros del mapa y las
/// consultas de "los mas urgentes primero" puedan ordenar por el enum sin un mapa
/// de traduccion adicional.
/// </summary>
public enum EstadoAbastecimiento
{
    /// <summary>Stock por encima del minimo. Se muestra en verde.</summary>
    Abastecido = 1,

    /// <summary>Stock por debajo del minimo pero aun cubre el consumo. Se muestra en naranja.</summary>
    Alerta = 2,

    /// <summary>Stock critico: no alcanza ni para el consumo del dia. Se muestra en rojo.</summary>
    Emergencia = 3
}
