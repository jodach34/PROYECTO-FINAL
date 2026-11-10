using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Productos.Dto;
using Rescauta.Application.Interfaces;

namespace Rescauta.Infrastructure.Products;

/// <summary>
/// Adaptador HTTP de Open Food Facts. Solo lee los campos que necesita el alta de insumos:
/// nombre, marca y cantidad. Cualquier fallo de la fuente externa se devuelve como
/// Result.Failure para que el registro manual del insumo siga disponible.
/// </summary>
public sealed class OpenFoodFactsClient(
    HttpClient httpClient,
    ILogger<OpenFoodFactsClient> logger) : IOpenFoodFactsClient
{
    private const string Campos = "product_name,product_name_es,generic_name_es,brands,quantity";

    private static readonly Regex CantidadRegex = new(
        @"(?<unidad>kilogramos?|kg|miligramos?|mg|gramos?|gr|g|mililitros?|ml|litros?|lt|l|centilitros?|cl|onzas?|oz|unidades?|uds?|u)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<Result<ProductoBarcodeDto>> BuscarPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        var codigoNormalizado = codigo?.Trim() ?? string.Empty;

        if (codigoNormalizado.Length is < 8 or > 14 || !codigoNormalizado.All(char.IsDigit))
        {
            return Result<ProductoBarcodeDto>.Failure(
                ProductoErrorCodes.CodigoInvalido,
                "El código de barras debe tener entre 8 y 14 dígitos.");
        }

        var ruta = $"api/v2/product/{Uri.EscapeDataString(codigoNormalizado)}.json?fields={Campos}";

        try
        {
            using var respuesta = await httpClient.GetAsync(ruta, cancellationToken);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return NoEncontrado();
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Open Food Facts respondio HTTP {Status} para el codigo {Codigo}.",
                    (int)respuesta.StatusCode,
                    codigoNormalizado);

                return NoDisponible();
            }

            await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(
                flujo,
                cancellationToken: cancellationToken);

            var raiz = documento.RootElement;

            if (raiz.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.Number &&
                status.GetInt32() != 1)
            {
                return NoEncontrado();
            }

            if (!raiz.TryGetProperty("product", out var producto) ||
                producto.ValueKind != JsonValueKind.Object)
            {
                return NoEncontrado();
            }

            var nombre = LeerTexto(producto, "product_name_es")
                ?? LeerTexto(producto, "product_name")
                ?? LeerTexto(producto, "generic_name_es");

            if (string.IsNullOrWhiteSpace(nombre))
            {
                return NoEncontrado();
            }

            var cantidad = LeerTexto(producto, "quantity");

            return Result<ProductoBarcodeDto>.Success(new ProductoBarcodeDto
            {
                Codigo = codigoNormalizado,
                Nombre = nombre,
                Marca = LeerMarca(producto),
                Cantidad = cantidad,
                UnidadSugerida = SugerirUnidad(cantidad)
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "No se pudo consultar Open Food Facts para el codigo {Codigo}.", codigoNormalizado);

            return NoDisponible();
        }
    }

    private static Result<ProductoBarcodeDto> NoEncontrado() =>
        Result<ProductoBarcodeDto>.Failure(
            ProductoErrorCodes.NoEncontrado,
            "No encontramos ese producto en Open Food Facts. Puedes registrar el insumo manualmente.");

    private static Result<ProductoBarcodeDto> NoDisponible() =>
        Result<ProductoBarcodeDto>.Failure(
            ProductoErrorCodes.NoDisponible,
            "No se pudo consultar Open Food Facts en este momento. Puedes registrar el insumo manualmente.");

    private static string? LeerTexto(JsonElement elemento, string propiedad) =>
        elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()?.Trim()
            : null;

    private static string? LeerMarca(JsonElement producto)
    {
        var marca = LeerTexto(producto, "brands");

        if (string.IsNullOrWhiteSpace(marca))
        {
            return null;
        }

        var primera = marca.Split(',')[0].Trim();

        return string.IsNullOrWhiteSpace(primera) ? null : primera;
    }

    private static string SugerirUnidad(string? cantidad)
    {
        if (string.IsNullOrWhiteSpace(cantidad))
        {
            return "unidad";
        }

        var coincidencia = CantidadRegex.Match(cantidad);

        if (!coincidencia.Success)
        {
            return "unidad";
        }

        return coincidencia.Groups["unidad"].Value.ToLowerInvariant() switch
        {
            "kilogramo" or "kilogramos" or "kg" => "kg",
            "miligramo" or "miligramos" or "mg" => "mg",
            "gramo" or "gramos" or "gr" or "g" => "g",
            "mililitro" or "mililitros" or "ml" => "ml",
            "litro" or "litros" or "lt" or "l" => "L",
            "centilitro" or "centilitros" or "cl" => "cl",
            "onza" or "onzas" or "oz" => "oz",
            "unidad" or "unidades" or "ud" or "uds" or "u" => "unidad",
            _ => "unidad"
        };
    }
}
