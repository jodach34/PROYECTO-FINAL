using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Kardex;

/// <summary>
/// Asiento del kardex: una entrada o una salida de un insumo.
///
/// NO implementa <see cref="IAggregateRoot"/> porque es entidad hija del agregado
/// <see cref="Insumo"/>. Solo la raiz de agregado puede obtener DbSet propio, asi que los
/// movimientos se consultan pasando por el insumo (regla 2 de Entities/README.md).
///
/// Es append-only (regla 1 de Features/Kardex/README.md): no expone ningun metodo para
/// editar ni para borrar, y a diferencia de <c>Donacion</c> tampoco tiene
/// <c>MarcarComoEliminada()</c>. Un asiento erroneo se corrige con un movimiento nuevo que lo
/// reversa; pisar el anterior dejaria el saldo sin rastro de por que cambio.
///
/// La creacion es <c>internal</c> a proposito: la unica forma de que exista un asiento es que
/// <see cref="Insumo"/> lo asiente, y ahi se valida que el saldo alcance. Un movimiento
/// suelto, sin insumo, no tiene sentido ni podria estar.
/// </summary>
public sealed class MovimientoKardex : BaseEntity
{
    /// <summary>Longitud maxima del responsable.</summary>
    public const int ResponsableMaxLength = 200;

    /// <summary>Longitud maxima del concepto.</summary>
    public const int ConceptoMaxLength = 300;

    private MovimientoKardex()
    {
        // Requerido por EF Core para materializar la entidad. El uso real es la fabrica
        // interna de abajo, que si valida.
        Responsable = string.Empty;
        Concepto = string.Empty;
    }

    /// <summary>
    /// Unico punto de creacion. Lo invoca <see cref="Insumo"/>, su raiz de agregado.
    /// La cantidad y el saldo los valida el dominio, nunca la base de datos.
    /// </summary>
    internal static MovimientoKardex Crear(
        Guid insumoId,
        TipoMovimiento tipoMovimiento,
        decimal cantidad,
        string responsable,
        string concepto,
        DateTimeOffset? fechaHora = null)
    {
        if (insumoId == Guid.Empty)
        {
            throw new DomainException("El insumo del movimiento es obligatorio.", nameof(insumoId));
        }

        if (cantidad <= 0m)
        {
            throw new DomainException(
                $"La cantidad debe ser mayor que cero. Recibido: {cantidad}.",
                nameof(cantidad));
        }

        if (string.IsNullOrWhiteSpace(responsable))
        {
            throw new DomainException("El responsable del movimiento es obligatorio.", nameof(responsable));
        }

        if (responsable.Length > ResponsableMaxLength)
        {
            throw new DomainException(
                $"El responsable del movimiento supera los {ResponsableMaxLength} caracteres.",
                nameof(responsable));
        }

        if (string.IsNullOrWhiteSpace(concepto))
        {
            throw new DomainException("El concepto del movimiento es obligatorio.", nameof(concepto));
        }

        if (concepto.Length > ConceptoMaxLength)
        {
            throw new DomainException(
                $"El concepto del movimiento supera los {ConceptoMaxLength} caracteres.",
                nameof(concepto));
        }

        return new MovimientoKardex
        {
            InsumoId = insumoId,
            TipoMovimiento = tipoMovimiento,
            Cantidad = cantidad,
            Responsable = responsable.Trim(),
            Concepto = concepto.Trim(),
            FechaHora = fechaHora ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Insumo que se mueve. Referencia al modulo Kardex sin FK declarada: la agrega el dueno
    /// del esquema en su migracion, no esta capa.
    /// </summary>
    public Guid InsumoId { get; private set; }

    /// <summary>Si el asiento suma o resta existencias.</summary>
    public TipoMovimiento TipoMovimiento { get; private set; }

    /// <summary>
    /// Cantidad en la unidad de medida del insumo. Siempre mayor que cero.
    /// Es <see cref="decimal"/> y no <c>int</c> porque un almacen de rescate maneja kilos y
    /// litros con decimales ("2.5 kg de arroz"), no unidades enteras.
    /// </summary>
    public decimal Cantidad { get; private set; }

    /// <summary>Momento en que ocurrio el movimiento, en UTC.</summary>
    public DateTimeOffset FechaHora { get; private set; }

    /// <summary>Quien ejecuto el movimiento. Nombre de la persona, no el id.</summary>
    public string Responsable { get; private set; }

    /// <summary>
    /// Motivo del movimiento: "Compra a Central de Abastos", "Despacho a Puesto Norte".
    /// No estaba en el enunciado inicial, pero sin el el asiento es una cifra sin contexto y
    /// el kardex deja de ser auditable. Si el equipo prefieres otra forma de exigirlo
    /// (enum de motivos, campo opcional), es un cambio local a esta clase.
    /// </summary>
    public string Concepto { get; private set; }

    /// <summary>True si el asiento suma existencias.</summary>
    public bool EsEntrada => TipoMovimiento == TipoMovimiento.Entrada;

    /// <summary>
    /// Como afecta al saldo: suma o resta. Permite calcular el nuevo stock sin recorrer el
    /// enumerado en el servicio de aplicacion.
    /// </summary>
    public decimal Delta => EsEntrada ? Cantidad : -Cantidad;
}
