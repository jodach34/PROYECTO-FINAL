namespace Rescauta.Domain.Enums;

/// <summary>
/// Naturaleza de un movimiento en el kardex. El kardex es append-only: un movimiento
/// no se edita ni se borra, una correccion se registra como un movimiento nuevo que
/// lo revierte.
/// </summary>
public enum TipoMovimientoKardex
{
    /// <summary>Suma existencias: donacion, compra municipal o ajuste por conteo fisico.</summary>
    Entrada = 1,

    /// <summary>Resta existencias: consumo diario de la cocina.</summary>
    Salida = 2,

    /// <summary>Correccion de inventario. Puede ser positiva o negativa.</summary>
    Ajuste = 3
}
