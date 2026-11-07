using System.Globalization;
using System.Net;
using System.Text.Json;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Services;

/// <summary>
/// Cliente del API publico de Open Food Facts. Se usa solo desde el formulario de alta de
/// insumos: si el servicio no responde, el registro manual sigue funcionando igual.
///
/// La respuesta se lee con JsonDocument, igual que OsrmRouteService, para quedarse solo con
/// los campos que el formulario necesita y tolerar que la ficha de un producto venga
/// incompleta. Un producto real puede no tener marca, ingredientes o tabla nutricional.
/// </summary>
public sealed class OpenFoodFactsService(HttpClient httpClient, ILogger<OpenFoodFactsService> logger)
{
    private const string Campos =
        "code,product_name,product_name_es,generic_name,generic_name_es,brands,quantity," +
        "ingredients_text,ingredients_text_es,image_front_small_url,image_url,nutriments";

    public async Task<Result<ProductoOpenFoodFacts>> BuscarPorCodigoAsync(
        string? codigo,
        CancellationToken cancellationToken = default)
    {
        var limpio = Normalizar(codigo);

        if (!EsCodigoValido(limpio))
        {
            return Result<ProductoOpenFoodFacts>.Failure(
                "codigo_invalido",
                "Ingresa un codigo de barras numerico de 8 a 14 digitos.");
        }

        var ruta = $"api/v2/product/{limpio}.json?fields={Uri.EscapeDataString(Campos)}";

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
                    limpio);

                return Result<ProductoOpenFoodFacts>.Failure(
                    "api_no_disponible",
                    "Open Food Facts no respondio correctamente. Puedes registrar el insumo manualmente.");
            }

            await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(
                flujo,
                cancellationToken: cancellationToken);

            var raiz = documento.RootElement;

            var status = raiz.TryGetProperty("status", out var statusElemento) &&
                         statusElemento.TryGetInt32(out var statusValor)
                ? statusValor
                : 0;

            if (!raiz.TryGetProperty("product", out var producto) ||
                producto.ValueKind != JsonValueKind.Object ||
                status != 1)
            {
                return NoEncontrado();
            }

            var nombre = Texto(producto, "product_name_es")
                ?? Texto(producto, "product_name")
                ?? Texto(producto, "generic_name_es")
                ?? Texto(producto, "generic_name");

            var marca = Texto(producto, "brands");
            var cantidad = Texto(producto, "quantity");
            var ingredientes = Texto(producto, "ingredients_text_es")
                ?? Texto(producto, "ingredients_text");
            var imagen = Texto(producto, "image_front_small_url")
                ?? Texto(producto, "image_url");

            var codigoProducto = Texto(producto, "code") ?? limpio;

            return Result<ProductoOpenFoodFacts>.Success(
                new ProductoOpenFoodFacts(
                    codigoProducto,
                    nombre,
                    marca,
                    cantidad,
                    ingredientes,
                    imagen,
                    LeerNutricion(producto)));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "No se pudo consultar Open Food Facts para el codigo {Codigo}.", limpio);

            return Result<ProductoOpenFoodFacts>.Failure(
                "api_no_disponible",
                "No se pudo conectar con Open Food Facts. Puedes registrar el insumo manualmente.");
        }
    }

    private static Result<ProductoOpenFoodFacts> NoEncontrado() =>
        Result<ProductoOpenFoodFacts>.Failure(
            "no_encontrado",
            "No se encontro un producto con ese codigo. Puedes registrarlo manualmente.");

    private static string Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return string.Empty;
        }

        // Los lectores suelen enviar espacios o guiones al copiar el codigo. Se ignoran,
        // pero cualquier otro caracter se conserva para que la validacion lo rechace.
        return new string(codigo
            .Trim()
            .Where(caracter => !char.IsWhiteSpace(caracter) && caracter != '-')
            .ToArray());
    }

    private static bool EsCodigoValido(string codigo) =>
        codigo.Length is >= 8 and <= 14 &&
        codigo.All(caracter => caracter is >= '0' and <= '9');

    private static string? Texto(JsonElement elemento, string propiedad)
    {
        if (!elemento.TryGetProperty(propiedad, out var valor) ||
            valor.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var texto = valor.GetString();

        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    private static IReadOnlyList<NutrienteResumen> LeerNutricion(JsonElement producto)
    {
        if (!producto.TryGetProperty("nutriments", out var nutrientes) ||
            nutrientes.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var campos = new (string Propiedad, string Etiqueta, string Unidad)[]
        {
            ("energy-kcal_100g", "Energia", "kcal"),
            ("fat_100g", "Grasas", "g"),
            ("saturated-fat_100g", "Grasas saturadas", "g"),
            ("carbohydrates_100g", "Carbohidratos", "g"),
            ("sugars_100g", "Azucares", "g"),
            ("proteins_100g", "Proteinas", "g"),
            ("salt_100g", "Sal", "g"),
            ("fiber_100g", "Fibra", "g")
        };

        var resultado = new List<NutrienteResumen>();

        foreach (var campo in campos)
        {
            if (LeerNumero(nutrientes, campo.Propiedad) is not { } numero)
            {
                continue;
            }

            resultado.Add(new NutrienteResumen(
                campo.Etiqueta,
                $"{numero.ToString("0.##", CultureInfo.InvariantCulture)} {campo.Unidad}"));
        }

        return resultado;
    }

    private static double? LeerNumero(JsonElement elemento, string propiedad)
    {
        if (!elemento.TryGetProperty(propiedad, out var valor))
        {
            return null;
        }

        if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDouble(out var numero))
        {
            return numero;
        }

        if (valor.ValueKind == JsonValueKind.String &&
            double.TryParse(
                valor.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var desdeTexto))
        {
            return desdeTexto;
        }

        return null;
    }
}
