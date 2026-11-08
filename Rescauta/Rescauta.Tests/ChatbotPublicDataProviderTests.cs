using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Features.Ai;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Domain.Entities.Comedores;
using Rescauta.Infrastructure.Persistence;
using Xunit;

namespace Rescauta.Tests;

public sealed class ChatbotPublicDataProviderTests
{
    [Fact]
    public async Task ObtenerDirectorio_SinComedores_DevuelveResultadoVacio()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await new ChatbotPublicDataProvider(database.Context)
            .ObtenerDirectorioComedoresAsync();

        Assert.Equal(0, result.TotalComedores);
        Assert.Empty(result.Comedores);
        Assert.Empty(result.Distritos);
    }

    [Fact]
    public async Task ObtenerDirectorio_MenosDeVeinte_OrdenaYProyectaSoloCamposPublicos()
    {
        await using var database = await CreateDatabaseAsync();
        database.Context.Set<Comedor>().AddRange(
            CreateComedor("Zeta", "Norte"),
            CreateComedor("Alfa", "Centro"),
            CreateComedor("Beta", "Centro"));
        await database.Context.SaveChangesAsync();

        var result = await new ChatbotPublicDataProvider(database.Context)
            .ObtenerDirectorioComedoresAsync();

        Assert.Equal(3, result.TotalComedores);
        Assert.Equal(new[] { "Alfa", "Beta", "Zeta" }, result.Comedores.Select(x => x.Nombre));
        Assert.Equal(new[] { "Centro", "Norte" }, result.Distritos);

        var publicItem = Assert.Single(result.Comedores, item => item.Nombre == "Alfa");
        Assert.Equal("Centro", publicItem.Distrito);
        var properties = typeof(ChatbotPublicComedorDto).GetProperties().Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "Nombre", "Distrito" }, properties);
    }

    [Fact]
    public async Task ObtenerDirectorio_MasDeVeinte_LimitaFilasPeroConservaTotalYDistritos()
    {
        await using var database = await CreateDatabaseAsync();
        var comedores = Enumerable.Range(1, 23)
            .Select(index => CreateComedor($"Comedor {index:00}", index == 23 ? "Sur" : "Centro"));
        database.Context.Set<Comedor>().AddRange(comedores);
        await database.Context.SaveChangesAsync();

        var result = await new ChatbotPublicDataProvider(database.Context)
            .ObtenerDirectorioComedoresAsync();

        Assert.Equal(23, result.TotalComedores);
        Assert.Equal(ChatbotPublicDataProvider.MaxComedores, result.Comedores.Count);
        Assert.Equal("Comedor 01", result.Comedores[0].Nombre);
        Assert.Equal("Comedor 20", result.Comedores[^1].Nombre);
        Assert.Equal(new[] { "Centro", "Sur" }, result.Distritos);
    }

    [Fact]
    public async Task ObtenerDirectorio_ExcluyeComedoresEliminadosLogicamente()
    {
        await using var database = await CreateDatabaseAsync();
        var visible = CreateComedor("Visible", "Centro");
        var deleted = CreateComedor("Eliminado", "Norte");
        deleted.MarcarComoEliminado();
        database.Context.Set<Comedor>().AddRange(visible, deleted);
        await database.Context.SaveChangesAsync();

        var result = await new ChatbotPublicDataProvider(database.Context)
            .ObtenerDirectorioComedoresAsync();

        Assert.Equal(1, result.TotalComedores);
        Assert.Equal("Visible", Assert.Single(result.Comedores).Nombre);
        Assert.Equal(new[] { "Centro" }, result.Distritos);
    }

    private static Comedor CreateComedor(string name, string district)
        => Comedor.Crear(name, district, 10, "Dirección de prueba", "555-0100", 10m, 20m);

    private static async Task<InMemoryDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new InMemoryDatabase(connection, context);
    }

    private sealed class InMemoryDatabase(
        SqliteConnection connection,
        AppDbContext context) : IAsyncDisposable
    {
        public AppDbContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
