using Rescauta.Domain.Common;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Exceptions;

namespace Rescauta.Domain.Entities;

/// <summary>
/// Raiz de agregado: una donacion de insumos registrada por un tercero.
///
/// Cruza con el modulo de inventario, y por eso es el unico punto del sistema con
/// riesgo de dependencia circular. La regla que lo evita: la donacion NUNCA escribe
/// un movimiento de kardex. Publica el evento <see cref="Events.DonacionConfirmada"/> y
/// es un handler de Application el que convierte la donacion en una entrada de
/// inventario. Asi Donaciones no importa nada de Kardex.
///
/// El codigo de seguimiento (DON-2026-00421) es lo que se imprime en el QR y lo que
/// el donante muestra al llegar al punto de recojo.
/// </summary>
public sealed class Donacion : BaseEntity, IAggregateRoot
{
    /// <summary>Codigo legible que viaja en el QR. Formato: DON-AAAA-NNNNN.</summary>
    public string CodigoSeguimiento { get; private set; } = string.Empty;

    public string NombreProducto { get; private set; } = string.Empty;

    public string Categoria { get; private set; } = string.Empty;

    public decimal Cantidad { get; private set; }

    public string UnidadMedida { get; private set; } = string.Empty;

    public DateOnly? CaducidadAprox { get; private set; }

    /// <summary>Direccion o punto de recojo declarado por el donante.</summary>
    public string DireccionRecojo { get; private set; } = string.Empty;

    /// <summary>Comedor al que se asigno. Null mientras la donacion no esta clasificada.</summary>
    public Guid? ComedorAsignadoId { get; private set; }

    public EstadoDonacion Estado { get; private set; } = EstadoDonacion.Recibida;

    /// <summary>Constructor para EF Core. No usar directamente.</summary>
    private Donacion()
    {
    }

    public Donacion(
        string codigoSeguimiento,
        string nombreProducto,
        string categoria,
        decimal cantidad,
        string unidadMedida,
        string direccionRecojo,
        DateOnly? caducidadAprox = null)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        {
            throw new DomainException("El codigo de seguimiento es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(nombreProducto))
        {
            throw new DomainException("El nombre del producto es obligatorio.");
        }

        if (cantidad <= 0m)
        {
            throw new DomainException($"La cantidad debe ser positiva. Recibido: {cantidad}.");
        }

        if (string.IsNullOrWhiteSpace(direccionRecojo))
        {
            throw new DomainException("La direccion o punto de recojo es obligatorio.");
        }

        CodigoSeguimiento = codigoSeguimiento.Trim().ToUpperInvariant();
        NombreProducto = nombreProducto.Trim();
        Categoria = categoria.Trim();
        Cantidad = cantidad;
        UnidadMedida = unidadMedida.Trim();
        DireccionRecojo = direccionRecojo.Trim();
        CaducidadAprox = caducidadAprox;
        Estado = EstadoDonacion.Recibida;
    }

    /// <summary>Revisa el insumo y confirma que puede entrar al inventario.</summary>
    public void Clasificar()
    {
        TransicionarDesde(EstadoDonacion.Recibida, nameof(Clasificar));
        Estado = EstadoDonacion.Clasificada;
    }

    /// <summary>
    /// Asigna la donacion a un comedor. Es el punto de no retorno logico: al asignar se
    /// publica <see cref="Events.DonacionConfirmada"/> y Kardex lo convierte en entrada.
    /// </summary>
    public void AsignarAComedor(Guid comedorId)
    {
        if (comedorId == Guid.Empty)
        {
            throw new DomainException("El comedor asignado no puede ser vacio.");
        }

        TransicionarDesde(EstadoDonacion.Clasificada, nameof(AsignarAComedor));

        ComedorAsignadoId = comedorId;
        Estado = EstadoDonacion.Asignada;
    }

    /// <summary>Cierra la donacion cuando el punto de recojo confirma la entrega.</summary>
    public void MarcarEntregada()
    {
        TransicionarDesde(EstadoDonacion.Asignada, nameof(MarcarEntregada));
        Estado = EstadoDonacion.Entregada;
    }

    /// <summary>
    /// Contenido que se codifica en el QR. Solo datos primitivos: el QR lo lee una
    /// persona con el telefono, no la aplicacion.
    /// </summary>
    public string ObtenerPayloadQr() =>
        $"RESCAUTA|{CodigoSeguimiento}|{NombreProducto}|{Cantidad} {UnidadMedida}";

    private void TransicionarDesde(EstadoDonacion esperado, string operacion)
    {
        if (Estado != esperado)
        {
            throw new DomainException(
                $"No se puede ejecutar '{operacion}': la donacion esta en '{Estado}' " +
                $"y se esperaba '{esperado}'.");
        }
    }
}
