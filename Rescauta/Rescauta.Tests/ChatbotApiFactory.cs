using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Tests;

/// <summary>
/// Fábrica de integración: levanta la API real con SQLite en memoria, migraciones
/// desactivadas y dobles de Gemini/Qdrant. No toca la base de desarrollo ni consume
/// cuota de los proveedores.
/// </summary>
internal sealed class ChatbotApiFactory : WebApplicationFactory<Program>
{
    public IGeminiClient Gemini { get; } = Substitute.For<IGeminiClient>();

    public IQdrantClient Qdrant { get; } = Substitute.For<IQdrantClient>();

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
                ["ConnectionStrings:RabbitMq"] = "",
                ["Cors:AllowAnyOrigin"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGeminiClient>();
            services.RemoveAll<IQdrantClient>();

            services.AddSingleton(Gemini);
            services.AddSingleton(Qdrant);
        });
    }
}
