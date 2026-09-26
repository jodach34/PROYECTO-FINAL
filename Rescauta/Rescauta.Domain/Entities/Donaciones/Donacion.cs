using Rescauta.Domain.Common;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Donaciones;

/// <summary>
/// Donacion de insumos a la red de rescate. Es raiz de agregado: el codigo de seguimiento y
/// el estado son invariantes que solo esta clase puede cambiar.
///
/// Invariantes:
///   * La cantidad es siempre mayor que cero.
///   * CodigoSeguimiento es unico en toda la tabla (lo indexa DonacionConfig).
///   * Un estado terminal (Entregada, Cancelada, Anulada) no admite transiciones.
///
/// Nota sobre InsumoId: es un Guid pelado, SIN navegacion ni clave foranea, a proposito.
/// La entidad Insumo pertenece al modulo Kardex, que todavia no existe. Si se declarara la
/// relacion ahora, el modulo Donaciones quedaria atado al esquema de Kardex y cada migracion
/// cruzaria ambos modulos. Cuando Kardex exista, el dueno del esquema agrega la FK en una
/// migracion propia.
///
/// Los setters son privados a proposito: el estado se cambia por metodos de negocio, nunca
/// con `donacion.Cantidad = 5` (ver Entities/README.md, regla 3).
/// </summary>
public sealed class Donacion : BaseEntity, IAggregateRoot
{
    /// <summary>Longitud maxima del codigo de seguimiento.</summary>
    public const int CodigoSeguimientoMaxLength = 32;

    /// <summary>Longitud maxima del nombre del donante.</summary>
    public const int DonanteMaxLength = 200;

    private Donacion()
    {
        // Requerido por EF Core para materializar la entidad. El uso real es la fabrica
        // estatico de abajo, que si valida.
        Donante = string.Empty;
        CodigoSeguimiento = string.Empty;
    }

    /// <summary>
    /// Crea una donacion en estado Pendiente. Es el unico punto de entrada valido: garantiza
    /// que nunca exista una donacion con cantidad cero, donante vacio o sin codigo.
    /// </summary>
    public static Donacion Registrar(
        Guid insumoId,
        int cantidad,
        string donante,
        string codigoSeguimiento,
        DateTimeOffset? fechaDonacion = null)
    {
        if (insumoId == Guid.Empty)
        {
            throw new DomainException("El insumo de la donacion es obligatorio.");
        }

        if (cantidad <= 0)
        {
            throw new DomainException(
                $"La cantidad debe ser mayor que cero. Recibido: {cantidad}.",
                nameof(cantidad));
        }

        if (string.IsNullOrWhiteSpace(donante))
        {
            throw new DomainException("El nombre del donante es obligatorio.", nameof(donante));
        }

        if (donante.Length > DonanteMaxLength)
        {
            throw new DomainException(
                $"El nombre del donante supera los {DonanteMaxLength} caracteres.",
                nameof(donante));
        }

        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            throw new DomainException("El codigo de seguimiento es obligatorio.", nameof(codigoSeguimiento));
        }

        if (codigoSeguimiento.Length > CodigoSeguimientoMaxLength)
        {
            throw new DomainException(
                $"El codigo de seguimiento supera los {CodigoSeguimientoMaxLength} caracteres.",
                nameof(codigoSeguimiento));
        }

        return new Donacion
        {
            InsumoId = insumoId,
            Cantidad = cantidad,
            Donante = donante.Trim(),
            CodigoSeguimiento = codigoSeguimiento.Trim(),
            FechaDonacion = fechaDonacion ?? DateTimeOffset.UtcNow,
            Estado = EstadoDonacion.Pendiente
        };
    }

    /// <summary>Insumo donado. Referencia al modulo Kardex, sin FK por ahora.</summary>
    public Guid InsumoId { get; private set; }

    /// <summary>Cantidad de unidades del insumo. Siempre mayor que cero.</summary>
    public int Cantidad { get; private set; }

    /// <summary>Momento en que se registro la donacion.</summary>
    public DateTimeOffset FechaDonacion { get; private set; }

    /// <summary>Nombre de quien dona.</summary>
    public string Donante { get; private set; }

    /// <summary>Estado del flujo de la donacion.</summary>
    public EstadoDonacion Estado { get; private set; }

    /// <summary>Codigo de seguimiento unico que ve el donante.</summary>
    public string CodigoSeguimiento { get; private set; }

    /// <summary>True si el estado ya no admite transiciones.</summary>
    public bool EsTerminal =>
        Estado is EstadoDonacion.Entregada or EstadoDonacion.Cancelada or EstadoDonacion.Anulada;

    /// <summary>La recepcion confirma que recibio los insumos.</summary>
    public void ConfirmarRecepcion()
    {
        TransicionarA(EstadoDonacion.Confirmada, EstadoDonacion.Pendiente);
    }

    /// <summary>La donacion sale hacia el punto de rescate.</summary>
    public void MarcarEnTransito()
    {
        TransicionarA(EstadoDonacion.EnTransito, EstadoDonacion.Confirmada);
    }

    /// <summary>El punto de rescate recibio la donacion. Estado final.</summary>
    public void MarcarEntregada()
    {
        TransicionarA(EstadoDonacion.Entregada, EstadoDonacion.EnTransito);
    }

    /// <summary>Se cancela antes de entregarla. Estado final.</summary>
    public void Cancelar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("Indicar el motivo de la cancelacion.", nameof(motivo));
        }

        if (EsTerminal)
        {
            throw new DomainException(
                $"No se puede cancelar una donacion en estado {Estado}.");
        }

        Estado = EstadoDonacion.Cancelada;
    }

    /// <summary>Se rechaza por no cumplir los criterios. Estado final.</summary>
    public void Anular(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("Indicar el motivo de la anulacion.", nameof(motivo));
        }

        if (EsTerminal)
        {
            throw new DomainException(
                $"No se puede anular una donacion en estado {Estado}.");
        }

        Estado = EstadoDonacion.Anulada;
    }

    /// <summary>
    /// Marca la donacion como borrada logicamente (BaseEntity.IsDeleted).
    ///
    /// IMPORTANTE: existe este metodo porque el borrado logico del proyecto NO es automatico.
    /// El filtro global de AppDbContext ya oculta las filas con IsDeleted, pero nada
    /// convierte un Remove() en borrado logico: AuditableEntityInterceptor solo rellena los
    /// campos de auditoria. Por eso hay que marcar la bandera y guardar, en vez de llamar a
    /// Remove(), que borra la fila de verdad y es irreversible.
    /// </summary>
    public void MarcarComoEliminada()
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Aplica una transicion validando el estado de origen. Centralizarlo aqui evita que cada
    /// metodo de negocio repita el chequeo de estado terminal.
    /// </summary>
    private void TransicionarA(EstadoDonacion destino, EstadoDonacion origenRequerido)
    {
        if (EsTerminal)
        {
            throw new DomainException(
                $"La donacion {CodigoSeguimiento} ya esta en un estado final ({Estado}) " +
                "y no admite mas transiciones.");
        }

        if (Estado != origenRequerido)
        {
            throw new DomainException(
                $"Transicion invalida: {Estado} -> {destino}. Se esperaba {origenRequerido}.");
        }

        Estado = destino;
    }
}
