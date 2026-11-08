using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Features.Ai.Validators;
using Xunit;

namespace Rescauta.Tests;

public sealed class PreguntaChatbotValidatorTests
{
    private static PreguntaChatbotValidator CreateValidator()
        => new(TestSupport.Options(new AiRagOptions
        {
            MinQuestionLength = 3,
            MaxQuestionLength = 50
        }));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void PreguntaVaciaOCorta_EsInvalida(string pregunta)
    {
        var result = CreateValidator().Validate(new PreguntaChatbotDto { Pregunta = pregunta });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PreguntaDemasiadoLarga_EsInvalida()
    {
        var result = CreateValidator().Validate(new PreguntaChatbotDto
        {
            Pregunta = new string('a', 51)
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PreguntaConCaracterDeControl_EsInvalida()
    {
        var result = CreateValidator().Validate(new PreguntaChatbotDto
        {
            Pregunta = "¿Qué es Rescauta?\u0001"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PreguntaValida_EsAceptada()
    {
        var result = CreateValidator().Validate(new PreguntaChatbotDto
        {
            Pregunta = "¿Cómo se registra una donación?"
        });

        Assert.True(result.IsValid);
    }
}
