using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Rescauta.Application.Features.Productos.Dto;
using Rescauta.Infrastructure.Products;
using Xunit;

namespace Rescauta.Tests;

public sealed class OpenFoodFactsClientTests
{
    private static OpenFoodFactsClient CreateClient(StubHttpMessageHandler handler)
        => new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://world.openfoodfacts.org/")
            },
            NullLogger<OpenFoodFactsClient>.Instance);

    [Fact]
    public async Task BuscarPorCodigo_ParseaNombreMarcaCantidadYUnidad()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""
            {
              "code": "3017620422003",
              "product": {
                "brands": "Ferrero, Nutella",
                "product_name": "Nutella",
                "product_name_es": "Nutella",
                "quantity": "400 g e"
              },
              "status": 1
            }
            """)));

        var resultado = await CreateClient(handler).BuscarPorCodigoAsync("3017620422003");

        Assert.True(resultado.IsSuccess);
        Assert.Equal("3017620422003", resultado.Value.Codigo);
        Assert.Equal("Nutella", resultado.Value.Nombre);
        Assert.Equal("Ferrero", resultado.Value.Marca);
        Assert.Equal("400 g e", resultado.Value.Cantidad);
        Assert.Equal("g", resultado.Value.UnidadSugerida);
    }

    [Fact]
    public async Task BuscarPorCodigo_ProductoInexistente_DevuelveNoEncontrado()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""{ "status": 0 }""")));

        var resultado = await CreateClient(handler).BuscarPorCodigoAsync("12345678");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ProductoErrorCodes.NoEncontrado, resultado.ErrorCode);
    }

    [Fact]
    public async Task BuscarPorCodigo_CodigoInvalido_NoLlamaAlServicio()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("No deberia llamarse."));

        var resultado = await CreateClient(handler).BuscarPorCodigoAsync("abc");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ProductoErrorCodes.CodigoInvalido, resultado.ErrorCode);
    }

    [Fact]
    public async Task BuscarPorCodigo_FalloDelServicio_DevuelveNoDisponible()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var resultado = await CreateClient(handler).BuscarPorCodigoAsync("3017620422003");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ProductoErrorCodes.NoDisponible, resultado.ErrorCode);
    }
}
