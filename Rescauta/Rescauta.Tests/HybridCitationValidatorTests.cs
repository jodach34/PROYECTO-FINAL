using Rescauta.Application.Features.Ai;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;
using Xunit;

namespace Rescauta.Tests;

public sealed class HybridCitationValidatorTests
{
    [Theory]
    [InlineData("La guía institucional explica cómo donar alimentos [S1]. Actualmente hay 5 comedores registrados [D1].")]
    [InlineData("## Donaciones\n- Para donar alimentos, consulta la guía institucional [S1].\n## Comedores\n- Hay cinco comedores registrados actualmente [D1].")]
    [InlineData("La guía explica cómo donar alimentos y el directorio registra comedores [S1][D1].")]
    [InlineData("La guía explica cómo donar alimentos [ S1 ] !\nActualmente hay cinco comedores registrados [ D 1 ] .")]
    [InlineData("La Sra. Vega puede donar alimentos [S1]. Actualmente hay 5 comedores registrados [D1].")]
    [InlineData("La guía explica, p. ej. cómo donar alimentos [S1]. Actualmente hay 5 comedores registrados [D1].")]
    [InlineData("La guía explica cómo donar alimentos y el directorio registra comedores [S1, D1].")]
    public void Validate_AceptaRespuestasConCoberturaYFuentesCorrectas(string answer)
    {
        var result = HybridCitationValidator.Validate(answer, Context(), PublicData());

        Assert.True(result.IsValid);
        Assert.Contains("S1", result.CitationIds);
        Assert.Contains("D1", result.CitationIds);
    }

    [Fact]
    public void Validate_AceptaEncabezadosDecorativosSinCita()
    {
        var result = HybridCitationValidator.Validate(
            "## Donaciones\nLa guía explica el proceso para donar alimentos [S1].\n## Directorio actual\nActualmente hay 5 comedores registrados [D1].",
            Context(),
            PublicData());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NoConfundeUnaAfirmacionExistencialConElTotalRegistrado()
    {
        var result = HybridCitationValidator.Validate(
            "Hay un comedor en Lurigancho-Chosica [D1].",
            Context(),
            PublicData());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("La guía explica cómo donar alimentos. Actualmente hay 5 comedores registrados.", HybridCitationRejectionReason.NoCitations)]
    [InlineData("La guía explica cómo donar alimentos [S1]. Actualmente hay 5 comedores registrados.", HybridCitationRejectionReason.FactualClaimWithoutCitation)]
    [InlineData("Actualmente hay 5 comedores registrados [S9].", HybridCitationRejectionReason.UnknownIdentifier)]
    [InlineData("Actualmente hay 5 comedores registrados [S1, fuente].", HybridCitationRejectionReason.UnsupportedFormat)]
    [InlineData("Actualmente [D1] hay 5 comedores registrados.", HybridCitationRejectionReason.UnsupportedFormat)]
    [InlineData("Actualmente hay 5 comedores registrados [S1].", HybridCitationRejectionReason.InsufficientSource)]
    [InlineData("Para donar alimentos, sigue el proceso indicado [D1].", HybridCitationRejectionReason.InsufficientSource)]
    [InlineData("Actualmente hay seis comedores registrados [D1].", HybridCitationRejectionReason.InsufficientSource)]
    public void Validate_RechazaCitasAusentesInvalidasODeAlcanceIncorrecto(
        string answer,
        HybridCitationRejectionReason expectedReason)
    {
        var result = HybridCitationValidator.Validate(answer, Context(), PublicData());

        Assert.False(result.IsValid);
        Assert.Equal(expectedReason, result.RejectionReason);
    }

    [Fact]
    public void Validate_RechazaViñetaFactualSinCitaAunqueOtraViñetaEsteCitada()
    {
        var answer = "- La guía explica cómo donar alimentos [S1]\n- Actualmente hay 5 comedores registrados";

        var result = HybridCitationValidator.Validate(answer, Context(), PublicData());

        Assert.False(result.IsValid);
        Assert.Equal(HybridCitationRejectionReason.FactualClaimWithoutCitation, result.RejectionReason);
    }

    [Fact]
    public void Validate_RechazaIdentificadorSinCorchetes()
    {
        var result = HybridCitationValidator.Validate(
            "La guía explica cómo donar alimentos S1.",
            Context(),
            PublicData());

        Assert.False(result.IsValid);
        Assert.Equal(HybridCitationRejectionReason.UnsupportedFormat, result.RejectionReason);
    }

    private static RagContext Context()
        => new()
        {
            Fragments =
            [
                new RagContextFragment
                {
                    CitationId = "S1",
                    Content = "Guía institucional para donar alimentos.",
                    Title = "Guía de donaciones",
                    Source = "guia-donaciones",
                    Score = 0.9
                }
            ]
        };

    private static ChatbotDynamicDataDto PublicData()
        => new()
        {
            TotalComedores = 5,
            Comedores = [new ChatbotPublicComedorDto { Nombre = "Comedor Las Palmeras", Distrito = "Lurigancho-Chosica" }],
            Distritos = ["Lurigancho-Chosica"]
        };
}
