using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities;

/// <summary>
/// Articulo de inventario asociado a un comedor: arroz, aceite, agua, gas, etc.
/// Es la fila que el Panel de Control muestra como "Stock de Arroz: 12 kg".
///
/// El stock NUNCA se edita con un setter. Todo cambio pasa por
/// <see cref="RegistrarEntrada"/>, <see cref="RegistrarSalida"/> o <see cref="Ajustar"/>,
/// y cada uno de esos metodos esta pensado para ir acompanado del movimiento de kardex
/// que deja rastro. Editar el stock sin movimiento rompe la trazabilidad.
/// </summary>
public sealed class Insumo : BaseEntity
{
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Ej.: "Alimentos Secos", "Agua Potable y Liquidos", "Balones de Gas".</summary>
    public string Categoria { get; private set; } = string.Empty;

    /// <summary>Ej.: "kg", "L", "Unidades (Sacos de 50kg)".</summary>
    public string UnidadMedida { get; private set; } = string.Empty;

    /// <summary>Comedor dueño de este stock. Sin el, el insumo no tiene donde usarse.</summary>
    public Guid ComedorId { get; private set; }

    public decimal StockActual { get; private set; }

    /// <summary>Umbral por debajo del cual el comedor pasa a Alerta.</summary>
    public decimal StockMinimo { get; private set; }

    /// <summary>
    /// Cuanto consume la cocina por dia. Es el divisor de UrgenciaCalculadorService:
    /// sin el no se pueden calcular los "dias de stock critico" del panel.
    /// </summary>
    public decimal ConsumoDiarioEstimado { get; private set; }

    public DateOnly? CaducidadAprox { get; private set; }

    /// <summary>Constructor para EF Core. No usar directamente.</summary>
    private Insumo()
    {
    }

    public Insumo(
        string nombre,
        string categoria,
        string unidadMedida,
        Guid comedorId,
        decimal consumoDiarioEstimado,
        decimal stockMinimo = 0m,
        DateOnly? caducidadAprox = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre del insumo es obligatorio.");
        }

        if (comedorId == Guid.Empty)
        {
            throw new DomainException("El insumo debe pertenecer a un comedor.");
        }

        if (consumoDiarioEstimado <= 0m)
        {
            throw new DomainException(
                $"El consumo diario estimado debe ser mayor que cero. Recibido: {consumoDiarioEstimado}.");
        }

        if (stockMinimo < 0m)
        {
            throw new DomainException($"El stock minimo no puede ser negativo. Recibido: {stockMinimo}.");
        }

        Nombre = nombre.Trim();
        Categoria = categoria.Trim();
        UnidadMedida = unidadMedida.Trim();
        ComedorId = comedorId;
        ConsumoDiarioEstimado = consumoDiarioEstimado;
        StockMinimo = stockMinimo;
        CaducidadAprox = caducidadAprox;
    }

    /// <summary>Suma existencias. Devuelve el stock resultante para el kardex.</summary>
    public decimal RegistrarEntrada(decimal cantidad)
    {
        if (cantidad <= 0m)
        {
            throw new DomainException($"La cantidad de entrada debe ser positiva. Recibido: {cantidad}.");
        }

        StockActual += cantidad;

        return StockActual;
    }

    /// <summary>
    /// Resta existencias por consumo. Falla si no hay stock suficiente: es preferible
    /// rechazar el movimiento a dejar el inventario en negativo, que despues rompe los
    /// calculos de dias restantes.
    /// </summary>
    public decimal RegistrarSalida(decimal cantidad)
    {
        if (cantidad <= 0m)
        {
            throw new DomainException($"La cantidad de salida debe ser positiva. Recibido: {cantidad}.");
        }

        if (cantidad > StockActual)
        {
            throw new DomainException(
                $"Stock insuficiente en '{Nombre}': se piden {cantidad} {UnidadMedida} " +
                $"y solo hay {StockActual} {UnidadMedida}.");
        }

        StockActual -= cantidad;

        return StockActual;
    }

    /// <summary>
    /// Correccion por conteo fisico. El delta puede ser negativo. Se prefiere registrar
    /// un movimiento de tipo Ajuste en el kardex antes de llamar a este metodo.
    /// </summary>
    public decimal Ajustar(decimal stockFisico)
    {
        if (stockFisico < 0m)
        {
            throw new DomainException($"El stock fisico no puede ser negativo. Recibido: {stockFisico}.");
        }

        StockActual = stockFisico;

        return StockActual;
    }

    /// <summary>
    /// Aplica una correccion por.delta con signo: positivo si el conteo fisico dio mas
    /// de lo que dice el sistema, negativo si dio menos (merma, mercaderia vencida).
    ///
    /// Es el metodo que usa el caso de uso del kardex para registrar un Ajuste, porque
    /// el movimiento necesita GUARDAR EL DELTA para que la suma del historial siga
    /// cuadrando con el stock. Si se usara <see cref="Ajustar"/> con el conteo absoluto,
    /// el kardex no tendria forma de saber si el ajuste sumo o resto.
    /// </summary>
    public decimal AjustarPorDelta(decimal delta)
    {
        if (delta == 0m)
        {
            throw new DomainException(
                "Un ajuste de cero no corrige nada. Registre solo la diferencia real entre " +
                "el conteo fisico y el stock del sistema.");
        }

        var stockResultante = StockActual + delta;

        if (stockResultante < 0m)
        {
            throw new DomainException(
                $"El ajuste de {delta} dejaria '{Nombre}' en negativo " +
                $"({stockResultante} {UnidadMedida}). Revise el conteo fisico.");
        }

        StockActual = stockResultante;

        return StockActual;
    }

    /// <summary>Actualiza la fecha de caducidad tras revisar el envase.</summary>
    public void ActualizarCaducidad(DateOnly? caducidadAprox)
    {
        CaducidadAprox = caducidadAprox;
    }
}
