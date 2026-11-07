namespace PROYECTO_FINAL.Models.Api;

/// <summary>
/// Respuesta de la busqueda de un producto por codigo de barras. El formulario de Kardex
/// solo usa <see cref="Nombre"/> para autocompletar un campo existente; el resto se muestra
/// como ficha de referencia y nunca se envia a la API de Rescauta, porque el insumo no tiene
/// columnas para marca, ingredientes ni nutricion.
/// </summary>
public sealed class ProductoBarcodeRespuesta
{
    public bool Encontrado { get; init; }

    /// <summary>codigo_invalido, no_encontrado, api_no_disponible...</summary>
    public string? Motivo { get; init; }

    public string Mensaje { get; init; } = string.Empty;

    public string? Codigo { get; init; }

    public string? Nombre { get; init; }

    public string? Marca { get; init; }

    public string? Cantidad { get; init; }

    public string? Ingredientes { get; init; }

    public string? ImagenUrl { get; init; }

    public IReadOnlyList<NutrienteResumen> Nutricion { get; init; } = [];
}

public sealed record NutrienteResumen(string Etiqueta, string Valor);

/// <summary>Producto ya normalizado desde la respuesta publica de Open Food Facts.</summary>
public sealed record ProductoOpenFoodFacts(
    string Codigo,
    string? Nombre,
    string? Marca,
    string? Cantidad,
    string? Ingredientes,
    string? ImagenUrl,
    IReadOnlyList<NutrienteResumen> Nutricion);
