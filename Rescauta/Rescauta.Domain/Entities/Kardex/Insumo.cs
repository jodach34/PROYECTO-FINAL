using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Kardex;

/// <summary>
/// Insumo del almacen: el bien que entra y sale del inventario (alimentos, agua, utiles).
/// Es raiz de agregado y duena de su kardex, porque el saldo y los movimientos son
/// invariantes que solo esta clase puede cambiar (reglas 2 y 3 de Entities/README.md).
///
/// Invariantes:
///   * Nombre y UnidadMedida son obligatorios y no pueden quedar en blanco.
///   * StockActual nunca es negativo: no se saca mas de lo que hay.
///   * StockActual es un valor derivado. Solo cambia al asentar un movimiento, nunca con
///     una asignacion directa (regla 2 de Features/Kardex/README.md).
///
/// Sobre el nombre: el README del modulo insinua "Articulo", pero la entidad
///     <c>Donacion</c> (modulo del otro dev) ya expone <c>Donacion.InsumoId</c>. Llamarla
///     <c>Insumo</c> es lo que hace que esa referencia pendiente apunte a algo real; cambiar el
///     nombre despues obligaria a tocar el modulo de Donaciones, que no es de este modulo.
///
/// Sobre <see cref="ComedorId"/>: el insumo NO es un stock global de almacen central sino la
/// despensa de un comedor concreto, que es como lo muestran las 3 pantallas (cada comedor con
/// su arroz, su agua y su gas, y su propio porcentaje de abastecimiento). La FK la define el
/// modulo Comedores, pero la consume este: <c>StockActual</c> sigue siendo el saldo de ESTE
/// comedor. El kardex de un comedor se lee filtrando por <see cref="ComedorId"/>.
/// </summary>
public sealed class Insumo : BaseEntity, IAggregateRoot
{
    /// <summary>Longitud maxima del nombre.</summary>
    public const int NombreMaxLength = 200;

    /// <summary>Longitud maxima de la unidad de medida.</summary>
    public const int UnidadMedidaMaxLength = 20;

    /// <summary>Concepto que se asienta en el movimiento de apertura.</summary>
    public const string ConceptoStockInicial = "Stock inicial";

    /// <summary>Responsable de los asientos que no hizo una persona (apertura, correcciones).</summary>
    public const string ResponsableSistema = "Sistema";

    private readonly List<MovimientoKardex> _movimientos = [];

    private Insumo()
    {
        // Requerido por EF Core para materializar la entidad. El uso real es la fabrica
        // estatica de abajo, que si valida.
        Nombre = string.Empty;
        UnidadMedida = string.Empty;
    }

    /// <summary>
    /// Da de alta un insumo en la despensa de un comedor. Si <paramref name="stockInicial"/> es
    /// mayor que cero, ademas asienta el movimiento de apertura, para que el saldo tenga
    /// siempre respaldo en el kardex y no sea un numero que aparecio de la nada.
    /// </summary>
    public static Insumo Crear(
        Guid comedorId,
        string nombre,
        string unidadMedida,
        decimal stockInicial = 0m,
        decimal stockMaximo = 0m)
    {
        if (comedorId == Guid.Empty)
        {
            throw new DomainException("El comedor del insumo es obligatorio.", nameof(comedorId));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre del insumo es obligatorio.", nameof(nombre));
        }

        if (nombre.Length > NombreMaxLength)
        {
            throw new DomainException(
                $"El nombre del insumo supera los {NombreMaxLength} caracteres.",
                nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(unidadMedida))
        {
            throw new DomainException("La unidad de medida es obligatoria.", nameof(unidadMedida));
        }

        if (unidadMedida.Length > UnidadMedidaMaxLength)
        {
            throw new DomainException(
                $"La unidad de medida supera los {UnidadMedidaMaxLength} caracteres.",
                nameof(unidadMedida));
        }

        if (stockInicial < 0m)
        {
            throw new DomainException(
                $"El stock inicial no puede ser negativo. Recibido: {stockInicial}.",
                nameof(stockInicial));
        }

        // Un tope menor que el stock inicial dejaria al comedor por encima del 100% para
        // siempre, y el porcentaje de abastecimiento es justo el numero que ordena el mapa.
        // Se corrige el tope en vez de rechazar: el inventario real manda sobre la config.
        var tope = stockMaximo < stockInicial ? stockInicial : stockMaximo;

        var insumo = new Insumo
        {
            ComedorId = comedorId,
            Nombre = nombre.Trim(),
            UnidadMedida = unidadMedida.Trim(),
            StockMaximo = tope
        };

        if (stockInicial > 0m)
        {
            insumo.RegistrarEntrada(
                stockInicial,
                ResponsableSistema,
                ConceptoStockInicial);
        }

        return insumo;
    }

    /// <summary>Comedor al que pertenece esta despensa. Obligatorio.</summary>
    public Guid ComedorId { get; private set; }

    /// <summary>Nombre del insumo.</summary>
    public string Nombre { get; private set; }

    /// <summary>
    /// Unidad en la que se mide el stock: "kg", "litro", "unidad", "paquete". Es texto y no
    /// enumerado para no tener que migrar la base cada vez que el almacen recibe un empaque
    /// nuevo; el normalizar a un catalogo corresponde a la capa Application.
    /// </summary>
    public string UnidadMedida { get; private set; }

    /// <summary>
    /// Existencia actual. Valor derivado de los movimientos: no se corrige a mano, se
    /// corrige con un movimiento que lo revierta.
    /// </summary>
    public decimal StockActual { get; private set; }

    /// <summary>
    /// Capacidad de la despensa: hasta donde se llena. El porcentaje de abastecimiento que
    /// pinta el mapa es <c>StockActual / StockMaximo</c>, asi que sin este numero el cliente
    /// no tendria de donde sacar el "15%" de la ficha. Cero significa "sin tope": el
    /// proveedor no reporta porcentaje, solo la cantidad.
    /// </summary>
    public decimal StockMaximo { get; private set; }

    /// <summary>
    /// Kardex del insumo, en orden de registro.
    ///
    /// Se expone SIN <c>AsReadOnly()</c> a proposito. EF Core rastrea los hijos de un
    /// agregado leyendo esta lista: si cada llamada devolviera una envoltura nueva, EF
    /// compararia contra un envoltorio distinto del que guardo como fotografia y daria por
    /// hecho que hay un asiento de mas, ya asientado, en vez de uno nuevo. El resultado era
    /// un UPDATE contra una fila que todavia no existia, o sea cero filas afectadas y un
    /// DbUpdateConcurrencyException que el servicio reportaba como "otro operacion actualizo
    /// este insumo" cuando en realidad no habia ninguna concurrencia.
    ///
    /// La lista sigue siendo de solo lectura para el codigo de aplicacion: la interfaz
    /// <see cref="IReadOnlyCollection{T}"/> no permite agregar ni quitar, y el campo privado
    /// no lo usa ningun modulo. Para cambiar el saldo hay que pasar por
    /// <see cref="RegistrarEntrada"/> o <see cref="RegistrarSalida"/>.
    /// </summary>
    public IReadOnlyCollection<MovimientoKardex> Movimientos => _movimientos;

    /// <summary>
    /// Da de alta existencias: compra, donacion, devolucion. Es el unico camino por el que
    /// <see cref="StockActual"/> puede subir.
    /// </summary>
    public MovimientoKardex RegistrarEntrada(
        decimal cantidad,
        string responsable,
        string concepto,
        DateTimeOffset? fechaHora = null)
    {
        return AsentarMovimiento(TipoMovimiento.Entrada, cantidad, responsable, concepto, fechaHora);
    }

    /// <summary>
    /// Despacha insumos: salida hacia un punto de rescate, consumo o merma. Es el unico
    /// camino por el que <see cref="StockActual"/> puede bajar, y rechaza el despacho si no
    /// alcanza el stock.
    /// </summary>
    public MovimientoKardex RegistrarSalida(
        decimal cantidad,
        string responsable,
        string concepto,
        DateTimeOffset? fechaHora = null)
    {
        return AsentarMovimiento(TipoMovimiento.Salida, cantidad, responsable, concepto, fechaHora);
    }

    /// <summary>
    /// Borrado logico (BaseEntity.IsDeleted).
    ///
    /// Es seguro a diferencia de la Donacion: desactivar el insumo esconde su saldo de las
    /// consultas, pero NO borra su kardex, que es el historico que la organizacion audita.
    /// Los asientos ya asentados no tienen MarcarComoEliminada() por eso mismo.
    /// </summary>
    public void MarcarComoEliminada()
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Valida, asienta el movimiento y descuenta o suma el saldo en la misma operacion. Que
    /// el asiento y el saldo se muevan juntos es justamente lo que garantiza la regla 2 del
    /// modulo: el saldo siempre es la suma de los movimientos.
    /// </summary>
    private MovimientoKardex AsentarMovimiento(
        TipoMovimiento tipo,
        decimal cantidad,
        string responsable,
        string concepto,
        DateTimeOffset? fechaHora)
    {
        if (tipo == TipoMovimiento.Salida && cantidad > StockActual)
        {
            throw new DomainException(
                $"No hay stock suficiente de {Nombre}. Se solicitan {cantidad} {UnidadMedida} " +
                $"y hay {StockActual} {UnidadMedida}.",
                nameof(cantidad));
        }

        var movimiento = MovimientoKardex.Crear(
            Id,
            tipo,
            cantidad,
            responsable,
            concepto,
            fechaHora);

        _movimientos.Add(movimiento);
        StockActual += movimiento.Delta;

        return movimiento;
    }
}
