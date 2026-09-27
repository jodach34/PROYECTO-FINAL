using Rescauta.Domain.Common;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities;

/// <summary>
/// Renglon del kardex: un movimiento de inventario con fecha, tipo, cantidad y
/// responsable.
///
/// Es la razon de ser del modulo: la trazabilidad. Reglas no negociables:
///   1. Es append-only. No hay setter, no hay Remove, no hay un metodo "Actualizar".
///      Una correccion se registra como un movimiento nuevo que lo revierte.
///   2. <see cref="Cantidad"/> es positiva, salvo en un <see cref="TipoMovimientoKardex.Ajuste"/>,
///      donde es un delta con signo. La excepcion es deliberada: un ajuste por conteo
///      fisico puede ser una merma, y si la cantidad fuera siempre positiva el kardex no
///      podria reconstruir el saldo. En los demas tipos el signo lo aporta
///      <see cref="Tipo"/>, para que un filtro por tipo no dependa del signo del numero.
///   3. <see cref="StockResultante"/> guarda el saldo DESPUES de aplicar el movimiento.
///      Es lo que permite auditar una partida sin recalcular todo el historico.
/// </summary>
public sealed class MovimientoKardex : BaseEntity
{
    public Guid InsumoId { get; private set; }

    public TipoMovimientoKardex Tipo { get; private set; }

    public decimal Cantidad { get; private set; }

    /// <summary>Saldo de existencias del insumo despues de aplicar este movimiento.</summary>
    public decimal StockResultante { get; private set; }

    public DateTimeOffset Fecha { get; private set; }

    /// <summary>Codigo de la donacion o nota que origino el movimiento. Ej.: "DON-2026-00421".</summary>
    public string? Referencia { get; private set; }

    /// <summary>Quien lo registro. Ej.: "Juana Quispe (Cocina)".</summary>
    public string? Responsable { get; private set; }

    /// <summary>Constructor para EF Core. No usar directamente.</summary>
    private MovimientoKardex()
    {
    }

    public MovimientoKardex(
        Guid insumoId,
        TipoMovimientoKardex tipo,
        decimal cantidad,
        decimal stockResultante,
        DateTimeOffset fecha,
        string? referencia = null,
        string? responsable = null)
    {
        if (insumoId == Guid.Empty)
        {
            throw new DomainException("El movimiento debe referenciar un insumo.");
        }

        // Un Ajuste es la unica magnitud que puede llegar negativa: representa la merma
        // detectada en el conteo fisico, y su valor ES el delta. El resto de tipos
        // conservan la regla de cantidad positiva, que es la que hace util filtrar por
        // tipo sin depender del signo del numero.
        if (tipo == TipoMovimientoKardex.Ajuste)
        {
            if (cantidad == 0m)
            {
                throw new DomainException("Un ajuste de cero no corrige nada. Ingrese la diferencia real.");
            }
        }
        else if (cantidad <= 0m)
        {
            throw new DomainException(
                $"La cantidad debe ser positiva para un movimiento de tipo {tipo}. Recibido: {cantidad}.");
        }

        if (stockResultante < 0m)
        {
            throw new DomainException(
                $"El stock resultante no puede ser negativo. Recibido: {stockResultante}.");
        }

        InsumoId = insumoId;
        Tipo = tipo;
        Cantidad = cantidad;
        StockResultante = stockResultante;
        Fecha = fecha;
        Referencia = string.IsNullOrWhiteSpace(referencia) ? null : referencia.Trim();
        Responsable = string.IsNullOrWhiteSpace(responsable) ? null : responsable.Trim();
    }

    /// <summary>True si el movimiento suma existencias.</summary>
    public bool EsEntrada => Tipo == TipoMovimientoKardex.Entrada;

    /// <summary>
    /// Delta con signo, listo para sumar sobre el stock anterior. Es una vista de
    /// lectura, no un setter: no permite modificar el movimiento.
    ///
    /// La suma de estos deltas sobre todos los movimientos de un insumo da su stock
    /// actual. Es la invariante que usa KardexRepository.CalcularStockAsync, y por eso
    /// Ajuste devuelve la cantidad tal cual (ya viene con signo) en vez de 0.
    /// </summary>
    public decimal DeltaConSigno() => Tipo switch
    {
        TipoMovimientoKardex.Entrada => Cantidad,
        TipoMovimientoKardex.Salida => -Cantidad,
        TipoMovimientoKardex.Ajuste => Cantidad,
        _ => 0m
    };
}
