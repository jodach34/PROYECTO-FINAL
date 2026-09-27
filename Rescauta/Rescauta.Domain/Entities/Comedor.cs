using Rescauta.Domain.Common;
using Rescauta.Domain.Enums;
using Rescauta.Domain.Exceptions;
using Rescauta.Domain.ValueObjects;

namespace Rescauta.Domain.Entities;

/// <summary>
/// Raiz de agregado: un comedor registrado que recibe raciones. Es la unidad que se
/// muestra en el mapa, la que tiene inventario propio y la que recibe donoraciones.
///
/// Reglas del dominio:
///   * El nombre y la direccion son obligatorios: sin ellos el punto no existe para el
///     donante ni para el repartidor.
///   * <see cref="Estado"/> no se asigna desde fuera. Lo recalcula la aplicacion con
///     UrgenciaCalculadorService y se cambia por <see cref="CambiarEstado"/>.
///   * La ubicacion es opcional, porque el cadastro se puede dar de alta antes de
///     geocodificar la direccion.
/// </summary>
public sealed class Comedor : BaseEntity, IAggregateRoot
{
    public string Nombre { get; private set; } = string.Empty;

    public string Distrito { get; private set; } = string.Empty;

    public string Direccion { get; private set; } = string.Empty;

    /// <summary>Telefono de la persona responsable en el punto. Opcional.</summary>
    public string? ContactoDirecto { get; private set; }

    public int RacionesDiarias { get; private set; }

    public UbicacionGeo? Ubicacion { get; private set; }

    public EstadoAbastecimiento Estado { get; private set; } = EstadoAbastecimiento.Abastecido;

    /// <summary>Constructor para EF Core. No usar directamente.</summary>
    private Comedor()
    {
    }

    public Comedor(
        string nombre,
        string distrito,
        string direccion,
        int racionesDiarias,
        UbicacionGeo? ubicacion = null,
        string? contactoDirecto = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre del comedor es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(distrito))
        {
            throw new DomainException("El distrito es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(direccion))
        {
            throw new DomainException("La direccion es obligatoria.");
        }

        if (racionesDiarias <= 0)
        {
            throw new DomainException(
                $"Las raciones diarias deben ser mayores que cero. Recibido: {racionesDiarias}.");
        }

        Nombre = nombre.Trim();
        Distrito = distrito.Trim();
        Direccion = direccion.Trim();
        RacionesDiarias = racionesDiarias;
        Ubicacion = ubicacion;
        ContactoDirecto = string.IsNullOrWhiteSpace(contactoDirecto) ? null : contactoDirecto.Trim();
    }

    /// <summary>
    /// Recalcula el estado de abastecimiento. Lo invoca UrgenciaCalculadorService
    /// cuando cambia el inventario del comedor, nunca desde un controller.
    /// </summary>
    public void CambiarEstado(EstadoAbastecimiento nuevoEstado)
    {
        if (nuevoEstado == EstadoAbastecimiento.Emergencia &&
            Estado != EstadoAbastecimiento.Emergencia)
        {
            // Hook para el evento de alerta. Se deja explicito para que el modulo de
            // tiempo real lo enganche sin tocar esta clase.
        }

        Estado = nuevoEstado;
    }

    /// <summary>Asocia o actualiza la geolocalizacion del punto.</summary>
    public void ActualizarUbicacion(UbicacionGeo ubicacion)
    {
        Ubicacion = ubicacion ?? throw new DomainException("La ubicacion no puede ser nula.");
    }

    /// <summary>Actualiza el telefono de contacto del punto.</summary>
    public void ActualizarContacto(string? contactoDirecto)
    {
        ContactoDirecto = string.IsNullOrWhiteSpace(contactoDirecto) ? null : contactoDirecto.Trim();
    }

    /// <summary>Corrige la cantidad de raciones que prepara la cocina cada dia.</summary>
    public void ActualizarRacionesDiarias(int racionesDiarias)
    {
        if (racionesDiarias <= 0)
        {
            throw new DomainException(
                $"Las raciones diarias deben ser mayores que cero. Recibido: {racionesDiarias}.");
        }

        RacionesDiarias = racionesDiarias;
    }
}
