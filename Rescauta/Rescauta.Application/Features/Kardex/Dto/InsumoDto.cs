namespace Rescauta.Application.Features.Kardex.Dto;

public sealed record CrearInsumoDto
{
    public Guid ComedorId { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string UnidadMedida { get; init; } = string.Empty;

    public decimal StockInicial { get; init; }

    public decimal StockMaximo { get; init; }
}

public sealed record InsumoResultDto
{
    public Guid InsumoId { get; init; }

    public Guid ComedorId { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string UnidadMedida { get; init; } = string.Empty;

    public decimal StockActual { get; init; }

    public decimal StockMaximo { get; init; }
}
