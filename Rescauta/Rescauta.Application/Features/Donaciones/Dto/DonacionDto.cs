using Rescauta.Domain.Entities.Donaciones;

namespace Rescauta.Application.Features.Donaciones.Dto;

/// <summary>Codigos de error del modulo Donaciones.</summary>
public static class DonacionErrorCodes
{
    public const string DonacionNoEncontrada = "donacion.no_encontrada";
    public const string InsumoNoEncontrado = "donacion.insumo_no_encontrado";
    public const string Validacion = "donacion.validacion";
    public const string SinPermiso = "donacion.sin_permiso";
    public const string EstadoInvalido = "donacion.estado_invalido";
    public const string ConflictoConcurrente = "donacion.conflicto_concurrente";
    public const string StockInsuficiente = "donacion.stock_insuficiente";
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

    /// <summary>Direccion o punto de recojo. Se guarda en la donacion y en el kardex.</summary>
    public string PuntoRecojo { get; init; } = string.Empty;
}

/// <summary>Motivo obligatorio para cancelar o anular una donacion.</summary>
public sealed record MotivoDonacionDto
{
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Filtros del listado administrativo de donaciones.</summary>
public sealed record DonacionFiltroDto
{
    public EstadoDonacion? Estado { get; init; }

    public Guid? ComedorId { get; init; }

    public Guid? InsumoId { get; init; }

    /// <summary>Busca por codigo de seguimiento o nombre del donante.</summary>
    public string? Busqueda { get; init; }

    public DateTimeOffset? Desde { get; init; }

    public DateTimeOffset? Hasta { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
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

    public string? PuntoRecojo { get; init; }

    /// <summary>Estado actual en texto, para que el cliente no conozca el enum.</summary>
    public string Estado { get; init; } = string.Empty;

    public DateTimeOffset FechaDonacion { get; init; }

    public DateTimeOffset? FechaConfirmacion { get; init; }

    public DateTimeOffset? FechaEnTransito { get; init; }

    public DateTimeOffset? FechaEntrega { get; init; }

    public DateTimeOffset? FechaCierre { get; init; }

    public string? MotivoCierre { get; init; }

    /// <summary>
    /// Stock del insumo despues de la operacion. En el registro todavia es el stock actual
    /// (la donacion queda pendiente); al confirmar refleja la entrada al kardex.
    /// </summary>
    public decimal StockResultante { get; init; }
}

/// <summary>Un cambio de estado auditado de una donacion.</summary>
public sealed record DonacionHistorialDto
{
    public string EstadoAnterior { get; init; } = string.Empty;

    public string EstadoNuevo { get; init; } = string.Empty;

    public string? Motivo { get; init; }

    public Guid? UsuarioId { get; init; }

    public DateTimeOffset Fecha { get; init; }
}
