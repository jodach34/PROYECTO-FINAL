namespace Rescauta.Application.Features.Productos.Dto;

public sealed record ProductoBarcodeDto
{
    public string Codigo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public string? Marca { get; init; }

    public string? Cantidad { get; init; }

    public string UnidadSugerida { get; init; } = "unidad";
}

public static class ProductoErrorCodes
{
    public const string CodigoInvalido = "producto.codigo_invalido";
    public const string NoEncontrado = "producto.no_encontrado";
    public const string NoDisponible = "producto.no_disponible";
}
