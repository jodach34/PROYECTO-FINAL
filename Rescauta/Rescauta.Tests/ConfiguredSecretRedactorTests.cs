using Rescauta.Infrastructure.Ai;
using Rescauta.Infrastructure.Options;
using Xunit;

namespace Rescauta.Tests;

public sealed class ConfiguredSecretRedactorTests
{
    private static ConfiguredSecretRedactor Create(
        string geminiKey = "",
        string qdrantKey = "")
        => new(
            TestSupport.Options(new GeminiOptions { ApiKey = geminiKey }),
            TestSupport.Options(new QdrantOptions { ApiKey = qdrantKey }));

    [Fact]
    public void ContainsSecret_DetectaClaveConfiguradaDeGemini()
    {
        var redactor = Create(geminiKey: "clave-gemini-de-laboratorio-123");

        Assert.True(redactor.ContainsSecret("texto con clave-gemini-de-laboratorio-123 dentro"));
    }

    [Fact]
    public void ContainsSecret_DetectaClaveConfiguradaDeQdrant()
    {
        var redactor = Create(qdrantKey: "clave-qdrant-de-laboratorio-456");

        Assert.True(redactor.ContainsSecret("texto con clave-qdrant-de-laboratorio-456 dentro"));
    }

    [Fact]
    public void ContainsSecret_DetectaPatronDeClaveGoogleAunqueNoEsteConfigurada()
    {
        var redactor = Create();
        var googleLike = "AIzaSyA1234567890abcdefghijklmnopqrstuv";

        Assert.True(redactor.ContainsSecret($"respuesta {googleLike}"));
    }

    [Fact]
    public void ContainsSecret_TextoNormal_EsFalso()
    {
        var redactor = Create(geminiKey: "clave-gemini-de-laboratorio-123");

        Assert.False(redactor.ContainsSecret("La documentación explica cómo donar alimentos."));
        Assert.False(redactor.ContainsSecret(string.Empty));
    }

    [Fact]
    public void Redact_ReemplazaClavesConfiguradasYPatrones()
    {
        var redactor = Create(
            geminiKey: "clave-gemini-de-laboratorio-123",
            qdrantKey: "clave-qdrant-de-laboratorio-456");

        var redacted = redactor.Redact(
            "gemini=clave-gemini-de-laboratorio-123 qdrant=clave-qdrant-de-laboratorio-456 " +
            "google=AIzaSyA1234567890abcdefghijklmnopqrstuv");

        Assert.DoesNotContain("clave-gemini-de-laboratorio-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-qdrant-de-laboratorio-456", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("AIzaSyA1234567890abcdefghijklmnopqrstuv", redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTADO]", redacted);
    }

    [Fact]
    public void Redact_TextoNormal_NoCambia()
    {
        var redactor = Create(geminiKey: "clave-gemini-de-laboratorio-123");
        const string text = "La documentación es pública y no contiene secretos.";

        Assert.Equal(text, redactor.Redact(text));
    }
}
