using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;
using Xunit;

namespace Rescauta.Tests;

public sealed class ChatbotApiTests
{
    private const string Endpoint = "/api/v1/chatbot/preguntar";

    [Fact]
    public async Task Preguntar_FlujoCompleto_DevuelveRespuestaYFuentesCitadas()
    {
        using var factory = new ChatbotApiFactory();
        SetupEmbedding(factory);
        SetupSearch(factory, TestSupport.Fragment(
            title: "Cómo donar",
            source: "donaciones",
            category: "donaciones",
            score: 0.93,
            content: "Para donar alimentos debes registrar cantidad, insumo y fecha de entrega."));
        SetupGeneration(factory, "Debes registrar cantidad, insumo y fecha de entrega [S1].");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Endpoint,
            new { pregunta = "¿Cómo registro una donación?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RespuestaChatbotDto>();

        Assert.NotNull(payload);
        Assert.True(payload!.SuficienteInformacion);
        var source = Assert.Single(payload.Fuentes);
        Assert.Equal("S1", source.Id);
        Assert.Equal("donaciones", source.Fuente);
        Assert.Equal("donaciones", source.Categoria);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("chunkId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("promptTokens", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidateTokens", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preguntar_PreguntaInvalida_Devuelve400SinConsultarProveedores()
    {
        using var factory = new ChatbotApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Endpoint,
            new { pregunta = "ab" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            ChatbotErrorCodes.PreguntaInvalida,
            problem.GetProperty("title").GetString());

        await factory.Qdrant.DidNotReceive().SearchAsync(
            Arg.Any<IReadOnlyList<float>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
        await factory.Gemini.DidNotReceive().GenerateEmbeddingAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_SinFragmentosRelevantes_DevuelveInsuficienteSinGenerar()
    {
        using var factory = new ChatbotApiFactory();
        SetupEmbedding(factory);
        SetupSearch(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Endpoint,
            new { pregunta = "¿Cuál es el procedimiento para donar?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RespuestaChatbotDto>();

        Assert.NotNull(payload);
        Assert.False(payload!.SuficienteInformacion);
        Assert.Empty(payload.Fuentes);
        Assert.Equal(
            "No encontré información suficiente en la documentación institucional de Rescauta para responder esa pregunta.",
            payload.Respuesta);

        await factory.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_ProveedorIndicaInsuficiente_DevuelveFuentesVacias()
    {
        using var factory = new ChatbotApiFactory();
        SetupEmbedding(factory);
        SetupSearch(factory, TestSupport.Fragment(
            content: "El registro de donaciones exige cantidad, insumo y fecha de recepción."));
        SetupGeneration(
            factory,
            "No encontré información suficiente en la documentación institucional de Rescauta para responder esa pregunta.");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Endpoint,
            new { pregunta = "¿Cómo se registra una donación?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RespuestaChatbotDto>();

        Assert.NotNull(payload);
        Assert.False(payload!.SuficienteInformacion);
        Assert.Empty(payload.Fuentes);
    }

    [Fact]
    public async Task Preguntar_TimeoutDelProveedor_Devuelve504SinDetallesInternos()
    {
        using var factory = new ChatbotApiFactory();
        SetupEmbedding(factory);
        SetupSearch(factory, TestSupport.Fragment(
            content: "El registro de donaciones exige cantidad, insumo y fecha de recepción."));
        factory.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiGenerationResult>(
                new AiProviderException(
                    AiFailureKind.Timeout,
                    "detalle-interno-que-no-debe-salir")));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Endpoint,
            new { pregunta = "¿Cómo se registra una donación?" });

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("detalle-interno-que-no-debe-salir", raw, StringComparison.Ordinal);
        Assert.Contains(ChatbotErrorCodes.AiTimeout, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_ContentTypeIncorrecto_Devuelve415()
    {
        using var factory = new ChatbotApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent(
            """{ "pregunta": "¿Cómo donar?" }""",
            Encoding.UTF8,
            "text/plain");

        var response = await client.PostAsync(Endpoint, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Preguntar_CuerpoExcesivo_Devuelve413SinError500()
    {
        using var factory = new ChatbotApiFactory();
        using var client = factory.CreateClient();

        var body = JsonSerializer.Serialize(new { pregunta = new string('a', 20_000) });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(Endpoint, content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Preguntar_JsonMalformado_Devuelve400()
    {
        using var factory = new ChatbotApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent("{ esto no es json", Encoding.UTF8, "application/json");

        var response = await client.PostAsync(Endpoint, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preguntar_CuerpoVacio_Devuelve400()
    {
        using var factory = new ChatbotApiFactory();
        using var client = factory.CreateClient();

        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(Endpoint, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preguntar_OnceSolicitudesEnUnMinuto_LaOnceavaDevuelve429()
    {
        using var factory = new ChatbotApiFactory();
        SetupEmbedding(factory);
        SetupSearch(factory);
        using var client = factory.CreateClient();

        for (var index = 0; index < 10; index++)
        {
            var response = await client.PostAsJsonAsync(
                Endpoint,
                new { pregunta = $"¿Pregunta número {index} sobre donaciones?" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(
            Endpoint,
            new { pregunta = "¿Pregunta once sobre donaciones?" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    private static void SetupEmbedding(ChatbotApiFactory factory)
    {
        factory.Gemini.GenerateEmbeddingAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float>>([0.1f, 0.2f, 0.3f]));
    }

    private static void SetupSearch(
        ChatbotApiFactory factory,
        params DocumentSearchResult[] fragments)
    {
        factory.Qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocumentSearchResult>>(fragments));
    }

    private static void SetupGeneration(ChatbotApiFactory factory, string text)
    {
        factory.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiGenerationResult(
                text,
                "STOP",
                PromptTokens: 100,
                CandidateTokens: 40,
                TotalTokens: 140)));
    }
}
