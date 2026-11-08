using Rescauta.Application.Features.Ai;
using Xunit;

namespace Rescauta.Tests;

public sealed class ChatbotQuestionRouterTests
{
    private readonly ChatbotQuestionRouter _router = new();

    [Theory]
    [InlineData("¿Qué comedores están registrados?", ChatbotPublicQueryKind.ListComedores)]
    [InlineData("Dime qué comedores están registrados en Rescauta", ChatbotPublicQueryKind.ListComedores)]
    [InlineData("¿Cuáles comedores están registrados?", ChatbotPublicQueryKind.ListComedores)]
    [InlineData("Cuáles son los comedores disponibles", ChatbotPublicQueryKind.ListComedores)]
    [InlineData("¿Cuántos comedores hay?", ChatbotPublicQueryKind.CountComedores)]
    [InlineData("Dime el número de comedores", ChatbotPublicQueryKind.CountComedores)]
    [InlineData("¿Cuántos comedores registrados hay?", ChatbotPublicQueryKind.CountComedores)]
    [InlineData("¿Cuántos comedores hay registrados?", ChatbotPublicQueryKind.CountComedores)]
    [InlineData("¿Cuántos comedores están disponibles?", ChatbotPublicQueryKind.CountComedores)]
    [InlineData("¿En qué distritos hay comedores?", ChatbotPublicQueryKind.DistrictsWithComedores)]
    [InlineData("Distritos con comedores", ChatbotPublicQueryKind.DistrictsWithComedores)]
    [InlineData("¿En qué distritos se encuentran los comedores?", ChatbotPublicQueryKind.DistrictsWithComedores)]
    [InlineData("¿En qué distritos hay comedores registrados?", ChatbotPublicQueryKind.DistrictsWithComedores)]
    public void Classify_PreguntaPublica_DevuelveConsultaCorrespondiente(
        string question,
        ChatbotPublicQueryKind expected)
    {
        Assert.Equal(expected, _router.Classify(question));
    }

    [Theory]
    [InlineData("¿Cómo funciona Rescauta?")]
    [InlineData("¿Cómo puedo donar alimentos y qué comedores están disponibles?")]
    [InlineData("¿Qué comedores tienen más stock?")]
    [InlineData("¿Cuál es el teléfono de los comedores?")]
    [InlineData("¿Qué operadores atienden los comedores?")]
    [InlineData("¿Cuál es el precio del bitcoin?")]
    [InlineData("Cuéntame algo sobre comedores")]
    [InlineData("¿Qué comedores están registrados y cuál es el bitcoin hoy?")]
    public void Classify_DocumentalPrivadaHibridaOAmbigua_NoActivaRutaPublica(string question)
    {
        Assert.Null(_router.Classify(question));
    }

    [Theory]
    [InlineData("¿Cómo puedo donar alimentos y qué comedores están registrados?")]
    [InlineData("¿Cómo funciona Rescauta y en qué distritos hay comedores?")]
    [InlineData("¿Qué requisitos existen para donar y cuántos comedores hay?")]
    [InlineData("Dime qué comedores están disponibles y cómo se hace una donación")]
    public void IsHybridQuestion_CombinaIntencionDocumentalYDirectorioPublico(string question)
        => Assert.True(_router.IsHybridQuestion(question));

    [Theory]
    [InlineData("¿Qué comedores están registrados?")]
    [InlineData("¿Cómo funciona Rescauta?")]
    [InlineData("¿Cómo puedo donar alimentos?")]
    [InlineData("Cuéntame algo sobre comedores")]
    [InlineData("¿Cómo puedo donar alimentos y qué comedores tienen stock?")]
    [InlineData("¿Cómo funciona Rescauta y cuál es el teléfono de los comedores?")]
    [InlineData("¿Qué requisitos existen para donar y cuál es mi donación?")]
    [InlineData("¿Cómo puedo donar y qué comedores hay junto con datos de donantes?")]
    [InlineData("¿Cómo funciona Rescauta y qué datos privados de operadores tienen?")]
    public void IsHybridQuestion_PreguntasDocumentalesPrivadasOPocoClaras_NoActivaRutaHibrida(string question)
        => Assert.False(_router.IsHybridQuestion(question));
}
