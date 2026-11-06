namespace Rescauta.Application.Features.Reportes.Dto;

/// <summary>Total de donaciones cuya recepcion ya fue confirmada.</summary>
public sealed record ReporteDonacionesRecibidasDto
{
    public int TotalDonaciones { get; init; }

    public decimal TotalCantidad { get; init; }
}

/// <summary>Stock disponible de un insumo, sumando los comedores visibles al usuario.</summary>
public sealed record ReporteStockInsumoDto
{
    public string Insumo { get; init; } = string.Empty;

    public string UnidadMedida { get; init; } = string.Empty;

    public decimal StockDisponible { get; init; }
}
