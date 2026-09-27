using Rescauta.Domain.Common;
using Rescauta.Domain.Entities.Kardex;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities.Comedores;

/// <summary>
/// Comedor (olla comun) que atiende la red de rescate. Es raiz de agregado: sus datos
/// catalogo son invariantes que solo esta clase puede cambiar.
///
/// Invariantes:
///   * Nombre, Distrito y Direccion son obligatorios.
///   * RacionesDiarias es mayor que cero: un comedor que no reparte no esta en el mapa.
///   * MapaX / MapaY estan entre 0 y 100: son porcentajes sobre el lienzo del SVG del mapa,
///     no coordenadas geograficas. Se guardan asi a proposito, para que el pin siga cayendo
///     en su sitio aunque el mapa se dibuje con otra relacion de aspecto.
///
/// NO tiene navigacion a <c>Insumo</c>. El insumo apunta al comedor (Insumo.ComedorId), no al
/// reves: el kardex duena de su saldo, y dar la vuelta a la relacion ataria los dos modulos.
/// Leer el inventario de un comedor es <c>Set&lt;Insumo&gt;().Where(i =&gt; i.ComedorId == id)</c>.
/// </summary>
public sealed class Comedor : BaseEntity, IAggregateRoot
{
    public const int NombreMaxLength = 200;
    public const int DistritoMaxLength = 120;
    public const int DireccionMaxLength = 300;
    public const int ContactoTelefonoMaxLength = 30;

    private readonly List<Insumo> _suministros = [];

    private Comedor()
    {
        // Requerido por EF Core para materializar la entidad. El uso real es la fabrica
        // estatica de abajo, que si valida.
        Nombre = string.Empty;
        Distrito = string.Empty;
        Direccion = string.Empty;
        ContactoTelefono = string.Empty;
    }

    /// <summary>Da de alta un comedor. Es el unico punto de entrada valido.</summary>
    public static Comedor Crear(
        string nombre,
        string distrito,
        int racionesDiarias,
        string direccion,
        string contactoTelefono,
        decimal mapaX,
        decimal mapaY)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre del comedor es obligatorio.", nameof(nombre));
        }

        if (nombre.Length > NombreMaxLength)
        {
            throw new DomainException(
                $"El nombre del comedor supera los {NombreMaxLength} caracteres.", nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(distrito))
        {
            throw new DomainException("El distrito del comedor es obligatorio.", nameof(distrito));
        }

        if (distrito.Length > DistritoMaxLength)
        {
            throw new DomainException(
                $"El distrito supera los {DistritoMaxLength} caracteres.", nameof(distrito));
        }

        if (racionesDiarias <= 0)
        {
            throw new DomainException(
                $"Las raciones diarias deben ser mayores que cero. Recibido: {racionesDiarias}.",
                nameof(racionesDiarias));
        }

        if (string.IsNullOrWhiteSpace(direccion))
        {
            throw new DomainException("La direccion del comedor es obligatoria.", nameof(direccion));
        }

        if (direccion.Length > DireccionMaxLength)
        {
            throw new DomainException(
                $"La direccion supera los {DireccionMaxLength} caracteres.", nameof(direccion));
        }

        if (mapaX is < 0m or > 100m)
        {
            throw new DomainException(
                $"La posicion horizontal del mapa debe estar entre 0 y 100. Recibido: {mapaX}.",
                nameof(mapaX));
        }

        if (mapaY is < 0m or > 100m)
        {
            throw new DomainException(
                $"La posicion vertical del mapa debe estar entre 0 y 100. Recibido: {mapaY}.",
                nameof(mapaY));
        }

        return new Comedor
        {
            Nombre = nombre.Trim(),
            Distrito = distrito.Trim(),
            RacionesDiarias = racionesDiarias,
            Direccion = direccion.Trim(),
            ContactoTelefono = (contactoTelefono ?? string.Empty).Trim(),
            MapaX = mapaX,
            MapaY = mapaY
        };
    }

    public string Nombre { get; private set; }

    public string Distrito { get; private set; }

    /// <summary>Raciones que el comedor reparte por dia. Es lo que ordena al mas urgente.</summary>
    public int RacionesDiarias { get; private set; }

    public string Direccion { get; private set; }

    public string ContactoTelefono { get; private set; }

    /// <summary>Posicion horizontal del pin, en porcentaje sobre el ancho del mapa (0-100).</summary>
    public decimal MapaX { get; private set; }

    /// <summary>Posicion vertical del pin, en porcentaje sobre el alto del mapa (0-100).</summary>
    public decimal MapaY { get; private set; }

    /// <summary>
    /// Da de baja al comedor. Es borrado logico: sus movimientos de kardex se conservan,
    /// porque son el historico que la organizacion audita. El filtro global de AppDbContext
    /// lo saca de los listados por <c>IsDeleted</c>.
    /// </summary>
    public void MarcarComoEliminado() => IsDeleted = true;

    /// <summary>
    /// Despensa del comedor. Solo lectura: el saldo de cada insumo lo mueve el agregado
    /// <c>Insumo</c>, nunca el comedor.
    /// </summary>
    public IReadOnlyCollection<Insumo> Suministros => _suministros.AsReadOnly();
}
