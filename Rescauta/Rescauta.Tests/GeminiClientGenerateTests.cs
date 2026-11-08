using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Rescauta.Application.Interfaces.Ai;
using Rescauta.Infrastructure.Ai;
using Rescauta.Infrastructure.Options;
using Xunit;

namespace Rescauta.Tests;

public sealed class GeminiClientGenerateTests
{
    private const string TestApiKey = "clave-de-prueba-123456789";

    private static GeminiClient CreateClient(
        StubHttpMessageHandler handler,
        string apiKey = TestApiKey,
        TimeSpan? timeout = null)
        => new(
            new HttpClient(handler)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(30)
            },
            TestSupport.Options(new GeminiOptions
            {
                ApiKey = apiKey,
                GenerationModel = "gemini-3.1-flash-lite"
            }),
            TestSupport.Options(new QdrantOptions()),
            NullLogger<GeminiClient>.Instance);

    private static AiGenerationRequest Request()
        => new()
        {
            SystemInstruction = "Reglas del sistema.",
            UserPrompt = "<pregunta>¿Cómo donar?</pregunta>",
            Temperature = 0.2,
            MaxOutputTokens = 300
        };

    [Fact]
    public async Task Generate_ParseaTextoFinishReasonYUsage()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""
            {
              "candidates": [
                {
                  "content": { "parts": [ { "text": "Hola " }, { "text": "mundo [S1]" } ] },
                  "finishReason": "STOP"
                }
              ],
              "usageMetadata": {
                "promptTokenCount": 12,
                "candidatesTokenCount": 34,
                "totalTokenCount": 46
              }
            }
            """)));

        var result = await CreateClient(handler).GenerateAsync(Request());

        Assert.Equal("Hola \nmundo [S1]", result.Text);
        Assert.Equal("STOP", result.FinishReason);
        Assert.Equal(12, result.PromptTokens);
        Assert.Equal(34, result.CandidateTokens);
        Assert.Equal(46, result.TotalTokens);
    }

    [Fact]
    public async Task Generate_EnviaInstruccionDeSistemaYClaveEnCabecera()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""
            {
              "candidates": [
                { "content": { "parts": [ { "text": "ok [S1]" } ] }, "finishReason": "STOP" }
              ]
            }
            """)));

        await CreateClient(handler).GenerateAsync(Request());

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValues("x-goog-api-key", out var values));
        Assert.Equal(TestApiKey, Assert.Single(values!));
        Assert.Contains("systemInstruction", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("Reglas del sistema.", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"maxOutputTokens\":300", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_PromptFeedbackBloqueado_LanzaBlocked()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""
            { "promptFeedback": { "blockReason": "SAFETY" } }
            """)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.Blocked, exception.Kind);
    }

    [Fact]
    public async Task Generate_FinishReasonSafetySinTexto_LanzaBlocked()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""
            {
              "candidates": [
                { "finishReason": "SAFETY", "content": { "parts": [] } }
              ]
            }
            """)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.Blocked, exception.Kind);
    }

    [Fact]
    public async Task Generate_SinCandidatos_LanzaInvalidResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""{ "candidates": [] }""")));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task Generate_JsonInvalido_LanzaInvalidResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("esto no es json")));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task Generate_Http429_LanzaQuotaExceeded()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""{ "error": "cuota" }""", HttpStatusCode.TooManyRequests)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.QuotaExceeded, exception.Kind);
    }

    [Fact]
    public async Task Generate_Http500_LanzaUpstreamErrorSinExponerElCuerpo()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse(
                """{ "error": "detalle-interno-del-proveedor" }""",
                HttpStatusCode.InternalServerError)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.UpstreamError, exception.Kind);
        Assert.DoesNotContain("detalle-interno-del-proveedor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_SinClave_LanzaNotConfiguredSinEnviarSolicitud()
    {
        var called = false;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            called = true;
            return Task.FromResult(TestSupport.JsonResponse("""{ "candidates": [] }"""));
        });

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler, apiKey: string.Empty).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.NotConfigured, exception.Kind);
        Assert.False(called);
    }

    [Fact]
    public async Task Generate_SinInstruccionDeSistema_LanzaArgumentException()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""{ "candidates": [] }""")));

        var request = Request() with { SystemInstruction = "   " };

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateClient(handler).GenerateAsync(request));
    }

    [Fact]
    public async Task Generate_TimeoutDelHttpClient_LanzaTimeout()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TestSupport.JsonResponse("""{ "candidates": [] }""");
        });

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler, timeout: TimeSpan.FromMilliseconds(50))
                .GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task Generate_CancelacionDelCliente_PropagaOperationCanceled()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TestSupport.JsonResponse("""{ "candidates": [] }""");
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(handler).GenerateAsync(Request(), cts.Token));
    }

    [Fact]
    public async Task Generate_RespuestaSinTexto_LanzaInvalidResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            TestSupport.JsonResponse("""
            {
              "candidates": [
                { "finishReason": "STOP", "content": { "parts": [] } }
              ]
            }
            """)));

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => CreateClient(handler).GenerateAsync(Request()));

        Assert.Equal(AiFailureKind.InvalidResponse, exception.Kind);
    }
}
