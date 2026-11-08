using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Rescauta.Application.Common;
using Rescauta.Application.Features.Donaciones;
using Rescauta.Application.Features.Donaciones.Dto;
using Xunit;

namespace Rescauta.Tests;

public sealed class DonacionesSeguimientoApiTests
{
    [Fact]
    public async Task Seguimiento_CodigoExistente_DevuelveSoloDatosPublicos()
    {
        using var factory = new DonacionesSeguimientoApiFactory();
        factory.Donaciones.ObtenerPorCodigoAsync(
                "DON-2024-00007",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<SeguimientoDonacionPublicoDto>.Success(
                new SeguimientoDonacionPublicoDto
                {
                    CodigoSeguimiento = "DON-2024-00007",
                    InsumoNombre = "Arroz",
                    ComedorNombre = "Comedor Central",
                    Cantidad = 12,
                    UnidadMedida = "kg",
                    Estado = "Confirmada",
                    FechaDonacion = new DateTimeOffset(2024, 5, 2, 10, 30, 0, TimeSpan.Zero)
                })));

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/donaciones/seguimiento/DON-2024-00007");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("DON-2024-00007", root.GetProperty("codigoSeguimiento").GetString());
        Assert.Equal("Confirmada", root.GetProperty("estado").GetString());
        Assert.Equal("Arroz", root.GetProperty("insumoNombre").GetString());
        Assert.Equal("Comedor Central", root.GetProperty("comedorNombre").GetString());
        Assert.Equal(12, root.GetProperty("cantidad").GetInt32());
        Assert.Equal("kg", root.GetProperty("unidadMedida").GetString());
        Assert.True(root.TryGetProperty("fechaDonacion", out _));

        foreach (var forbidden in new[]
                 {
                     "donante", "puntoRecojo", "latitudOrigen", "longitudOrigen",
                     "stockResultante", "insumoId", "comedorId", "motivoCierre"
                 })
        {
            Assert.False(root.TryGetProperty(forbidden, out _), $"La respuesta expuso {forbidden}.");
        }

        await factory.Donaciones.Received(1).ObtenerPorCodigoAsync(
            "DON-2024-00007",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Seguimiento_CodigoInexistente_Devuelve404Controlado()
    {
        using var factory = new DonacionesSeguimientoApiFactory();
        factory.Donaciones.ObtenerPorCodigoAsync(
                "DON-2024-99999",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<SeguimientoDonacionPublicoDto>.Failure(
                DonacionErrorCodes.DonacionNoEncontrada,
                "No encontramos una donacion con ese codigo.")));

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/donaciones/seguimiento/DON-2024-99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Donante", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PuntoRecojo", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListadoDeDonaciones_SigueProtegido()
    {
        using var factory = new DonacionesSeguimientoApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/donaciones?limite=5");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class DonacionesSeguimientoApiFactory : WebApplicationFactory<Program>
    {
        public IDonacionService Donaciones { get; } = Substitute.For<IDonacionService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:ConnectionString"] = "Data Source=:memory:",
                    ["Database:MigrateOnStartup"] = "false",
                    ["RabbitMq:Enabled"] = "false",
                    ["ConnectionStrings:Redis"] = "",
                    ["ConnectionStrings:RabbitMq"] = ""
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDonacionService>();
                services.AddSingleton(Donaciones);
            });
        }
    }
}
