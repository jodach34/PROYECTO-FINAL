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
///   * El stock del insumo recien se mueve cuando la donacion pasa a Confirmada.
///
/// Nota sobre InsumoId y ComedorId: son Guid pelados, SIN navegacion ni clave foranea, a
/// proposito. Las entidades Insumo y Comedor pertenecen a otros modulos. Los nombres se
/// guardan como snapshot para que el historial no dependa de que el insumo siga existiendo.
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

    /// <summary>Longitud maxima del nombre del insumo.</summary>
    public const int InsumoNombreMaxLength = 200;

    /// <summary>Longitud maxima de la unidad de medida.</summary>
    public const int UnidadMedidaMaxLength = 20;

    /// <summary>Longitud maxima del nombre del comedor.</summary>
    public const int ComedorNombreMaxLength = 200;

    /// <summary>Longitud maxima del punto de recojo.</summary>
    public const int PuntoRecojoMaxLength = 300;

    /// <summary>Longitud maxima del motivo de cierre.</summary>
    public const int MotivoCierreMaxLength = 500;

    private Donacion()
    {
        // Requerido por EF Core para materializar la entidad. El uso real es la fabrica
        // estatico de abajo, que si valida.
        Donante = string.Empty;
        CodigoSeguimiento = string.Empty;
        InsumoNombre = string.Empty;
        UnidadMedida = string.Empty;
        ComedorNombre = string.Empty;
    }

    /// <summary>
    /// Crea una donacion en estado Pendiente. Es el unico punto de entrada valido: garantiza
    /// que nunca exista una donacion con cantidad cero, donante vacio o sin codigo.
    ///
    /// La donacion NO mueve stock aqui. Eso ocurre al confirmar la recepcion, cuando la
    /// coordinacion verifica que los insumos llegaron fisicamente.
    /// </summary>
    public static Donacion Registrar(
        Guid insumoId,
        int cantidad,
        string donante,
        string codigoSeguimiento,
        Guid comedorId,
        string insumoNombre,
        string unidadMedida,
        string comedorNombre,
        string? puntoRecojo = null,
        DateTimeOffset? fechaDonacion = null,
        Guid? usuarioId = null)
    {
        if (insumoId == Guid.Empty)
        {
            throw new DomainException("El insumo de la donacion es obligatorio.");
        }

        if (comedorId == Guid.Empty)
        {
            throw new DomainException("El comedor de la donacion es obligatorio.");
        }

        if (cantidad <= 0)
        {
            throw new DomainException(
                $"La cantidad debe ser mayor que cero. Recibido: {cantidad}.",
                nameof(cantidad));
        }

        ValidarTextoRequerido(donante, DonanteMaxLength, "El nombre del donante", nameof(donante));
        ValidarTextoRequerido(
            insumoNombre,
            InsumoNombreMaxLength,
            "El nombre del insumo",
            nameof(insumoNombre));
        ValidarTextoRequerido(
            unidadMedida,
            UnidadMedidaMaxLength,
            "La unidad de medida",
            nameof(unidadMedida));
        ValidarTextoRequerido(
            comedorNombre,
            ComedorNombreMaxLength,
            "El nombre del comedor",
            nameof(comedorNombre));
        ValidarTextoRequerido(
            codigoSeguimiento,
            CodigoSeguimientoMaxLength,
            "El codigo de seguimiento",
            nameof(codigoSeguimiento));

        var fecha = fechaDonacion ?? DateTimeOffset.UtcNow;

        return new Donacion
        {
            InsumoId = insumoId,
            ComedorId = comedorId,
            InsumoNombre = insumoNombre.Trim(),
            UnidadMedida = unidadMedida.Trim(),
            ComedorNombre = comedorNombre.Trim(),
            Cantidad = cantidad,
            Donante = donante.Trim(),
            CodigoSeguimiento = codigoSeguimiento.Trim(),
            PuntoRecojo = NormalizarPuntoRecojo(puntoRecojo),
            FechaDonacion = fecha,
            FechaDonacionUtc = fecha.UtcDateTime,
            Estado = EstadoDonacion.Pendiente,
            UsuarioId = usuarioId
        };
    }

    /// <summary>Insumo donado. Referencia al modulo Kardex, sin FK por ahora.</summary>
    public Guid InsumoId { get; private set; }

    /// <summary>Comedor destino. Se resuelve del insumo al registrar.</summary>
    public Guid ComedorId { get; private set; }

    /// <summary>Snapshot del nombre del insumo para que el historial no quede vacio.</summary>
    public string InsumoNombre { get; private set; }

    /// <summary>Snapshot de la unidad de medida del insumo.</summary>
    public string UnidadMedida { get; private set; }

    /// <summary>Snapshot del nombre del comedor destino.</summary>
    public string ComedorNombre { get; private set; }

    /// <summary>Punto donde se recogen los insumos. Opcional.</summary>
    public string? PuntoRecojo { get; private set; }

    /// <summary>Cantidad de unidades del insumo. Siempre mayor que cero.</summary>
    public int Cantidad { get; private set; }

    /// <summary>Momento en que se registro la donacion.</summary>
    public DateTimeOffset FechaDonacion { get; private set; }

    /// <summary>
    /// La misma fecha en UTC como DateTime. Existe porque SQLite no ordena por
    /// DateTimeOffset; los listados paginados ordenan por esta columna.
    /// </summary>
    public DateTime FechaDonacionUtc { get; private set; }

    /// <summary>Nombre de quien dona.</summary>
    public string Donante { get; private set; }

    /// <summary>Estado del flujo de la donacion.</summary>
    public EstadoDonacion Estado { get; private set; }

    /// <summary>Codigo de seguimiento unico que ve el donante.</summary>
    public string CodigoSeguimiento { get; private set; }

    public Guid? UsuarioId { get; private set; }

    /// <summary>Fecha en que la coordinacion confirmo la recepcion.</summary>
    public DateTimeOffset? FechaConfirmacion { get; private set; }

    /// <summary>Fecha en que la donacion salio hacia el comedor.</summary>
    public DateTimeOffset? FechaEnTransito { get; private set; }

    /// <summary>Fecha en que el comedor recibio la donacion.</summary>
    public DateTimeOffset? FechaEntrega { get; private set; }

    /// <summary>Fecha en que se cancelo o anulo la donacion.</summary>
    public DateTimeOffset? FechaCierre { get; private set; }

    /// <summary>Motivo por el que se cancelo o anulo. Null mientras siga abierta.</summary>
    public string? MotivoCierre { get; private set; }

    /// <summary>True si el estado ya no admite transiciones.</summary>
    public bool EsTerminal =>
        Estado is EstadoDonacion.Entregada or EstadoDonacion.Cancelada or EstadoDonacion.Anulada;

    /// <summary>True si esta donacion ya movio stock al inventario.</summary>
    public bool StockIngresado =>
        Estado is EstadoDonacion.Confirmada or EstadoDonacion.EnTransito or EstadoDonacion.Entregada;

    /// <summary>La recepcion confirma que recibio los insumos y dispara la entrada al kardex.</summary>
    public void ConfirmarRecepcion(DateTimeOffset? fecha = null)
    {
        TransicionarA(EstadoDonacion.Confirmada, EstadoDonacion.Pendiente);
        FechaConfirmacion = fecha ?? DateTimeOffset.UtcNow;
    }

    /// <summary>La donacion sale hacia el punto de rescate.</summary>
    public void MarcarEnTransito(DateTimeOffset? fecha = null)
    {
        TransicionarA(EstadoDonacion.EnTransito, EstadoDonacion.Confirmada);
        FechaEnTransito = fecha ?? DateTimeOffset.UtcNow;
    }

    /// <summary>El punto de rescate recibio la donacion. Estado final.</summary>
    public void MarcarEntregada(DateTimeOffset? fecha = null)
    {
        TransicionarA(EstadoDonacion.Entregada, EstadoDonacion.EnTransito);
        FechaEntrega = fecha ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Se cancela antes de entregarla. Estado final.</summary>
    public void Cancelar(string motivo, DateTimeOffset? fecha = null)
    {
        ValidarMotivo(motivo, "cancelacion");

        if (EsTerminal)
        {
            throw new DomainException(
                $"No se puede cancelar una donacion en estado {Estado}.");
        }

        Estado = EstadoDonacion.Cancelada;
        FechaCierre = fecha ?? DateTimeOffset.UtcNow;
        MotivoCierre = motivo.Trim();
    }

    /// <summary>Se rechaza por no cumplir los criterios. Estado final.</summary>
    public void Anular(string motivo, DateTimeOffset? fecha = null)
    {
        ValidarMotivo(motivo, "anulacion");

        if (EsTerminal)
        {
            throw new DomainException(
                $"No se puede anular una donacion en estado {Estado}.");
        }

        Estado = EstadoDonacion.Anulada;
        FechaCierre = fecha ?? DateTimeOffset.UtcNow;
        MotivoCierre = motivo.Trim();
    }

    /// <summary>
    /// Marca la donacion como borrada logicamente (BaseEntity.IsDeleted).
    ///
    /// Solo se permite mientras no haya stock comprometido: una donacion confirmada, en
    /// transito o entregada debe conservarse para que el kardex siga siendo auditable.
    /// </summary>
    public void MarcarComoEliminada()
    {
        if (StockIngresado)
        {
            throw new DomainException(
                "No se puede eliminar una donacion que ya ingreso stock al inventario.");
        }

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

    private static void ValidarTextoRequerido(
        string valor,
        int maxLength,
        string nombreCampo,
        string parametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException($"{nombreCampo} es obligatorio.", parametro);
        }

        if (valor.Length > maxLength)
        {
            throw new DomainException(
                $"{nombreCampo} supera los {maxLength} caracteres.",
                parametro);
        }
    }

    private static void ValidarMotivo(string motivo, string accion)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException(
                $"Indicar el motivo de la {accion}.",
                nameof(motivo));
        }

        if (motivo.Length > MotivoCierreMaxLength)
        {
            throw new DomainException(
                $"El motivo de la {accion} supera los {MotivoCierreMaxLength} caracteres.",
                nameof(motivo));
        }
    }

    private static string? NormalizarPuntoRecojo(string? puntoRecojo)
    {
        if (string.IsNullOrWhiteSpace(puntoRecojo))
        {
            return null;
        }

        if (puntoRecojo.Length > PuntoRecojoMaxLength)
        {
            throw new DomainException(
                $"El punto de recojo supera los {PuntoRecojoMaxLength} caracteres.",
                nameof(puntoRecojo));
        }

        return puntoRecojo.Trim();
    }
}
