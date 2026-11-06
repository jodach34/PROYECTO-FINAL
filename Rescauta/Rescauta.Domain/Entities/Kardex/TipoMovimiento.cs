namespace Rescauta.Domain.Entities.Kardex;

/// <summary>
/// Sentido del asiento que se asienta en el kardex.
///
/// No se Incluye un tipo "Ajuste" a proposito: la regla 1 del modulo Kardex pide que el
/// historico sea inmutable, y un ajuste se modela como un par de movimientos (una salida y
/// una entrada) que se referencian entre si. Agregar un tercer valor aqui abriria la puerta
/// a "corregir" el saldo con un solo asiento, que es justo lo que se quiere evitar.
///
/// Se persiste como texto (ver la configuracion EF de la Fase 3): "Entrada" y "Salida" se leen
/// mejor que 0 y 1 en una auditoria, que en Rescauta se revisa a mano.
/// </summary>
public enum TipoMovimiento
{
    /// <summary>Suma existencias: compra, donacion, devolucion, ajuste de cierre.</summary>
    Entrada = 0,

    /// <summary>Resta existencias: despacho a un punto de rescate, consumo, merma.</summary>
    Salida = 1
}
