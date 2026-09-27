using Rescauta.Application.Common;

namespace Rescauta.Application.Features.Donaciones.Dto;

/// <summary>Codigos de error del modulo Donaciones.</summary>
public static class DonacionErrorCodes
{
    public const string DonacionNoEncontrada = "donacion.no_encontrada";
    public const string InsumoNoEncontrado = "donacion.insumo_no_encontrado";
    public const string Validacion = "donacion.validacion";
    public const string ErrorInterno = "donacion.error_interno";
}

/// <summary>
/// Lo que llega del formulario del Banco de Alimentos. El codigo de seguimiento NO se
/// recibe: lo genera el servidor, porque es el indice unico de la tabla y si lo mandara el
/// cliente bastaria con repetirlo para chocar dos donaciones.
/// </summary>
public sealed record RegistrarDonacionDto
{
    /// <summary>Insumo que se dona. Su comedor es el destino.</summary>
    public Guid InsumoId { get; init; }

    public int Cantidad { get; init; }

    /// <summary>Quien dona. El dominio lo exige y no admite cadenas vacias.</summary>
    public string Donante { get; init; } = string.Empty;

    /// <summary>Direccion o punto de recojo. Va al concepto del asiento del kardex.</summary>
    public string PuntoRecojo { get; init; } = string.Empty;
}

/// <summary>Donacion registrada, con lo que el cliente necesita para pintar la confirmacion.</summary>
public sealed record DonacionResultDto
{
    public Guid DonacionId { get; init; }

    /// <summary>Codigo unico que ve el donante. Formato DON-YYYY-NNNNN.</summary>
    public string CodigoSeguimiento { get; init; } = string.Empty;

    public Guid InsumoId { get; init; }

    public string InsumoNombre { get; init; } = string.Empty;

    /// <summary>Comedor destino. Se resuelve del insumo, el cliente no lo elige a mano.</summary>
    public Guid ComedorId { get; init; }

    public string ComedorNombre { get; init; } = string.Empty;

    public int Cantidad { get; init; }

    public string UnidadMedida { get; init; } = string.Empty;

    public string Donante { get; init; } = string.Empty;

    /// <summary>Estado inicial, "Pendiente". Texto porque lo pinta el cliente.</summary>
    public string Estado { get; init; } = string.Empty;

    public DateTimeOffset FechaDonacion { get; init; }

    /// <summary>Stock del insumo despues de asentar la entrada. Va incluido para que el
    /// cliente no tenga que hacer una segunda peticion solo por el numero nuevo.</summary>
    public decimal StockResultante { get; init; }
}
