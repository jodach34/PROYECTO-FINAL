using System.Text.Json;
using NSubstitute;
using Rescauta.Application.Features.Ai;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Features.Ai.Validators;
using Rescauta.Application.Interfaces.Ai;
using Xunit;

namespace Rescauta.Tests;

public sealed class RagAnswerServiceTests
{
    [Fact]
    public async Task Preguntar_ConsultaPublicaDeComedores_RespondeConDirectorioSinRag()
    {
        using var harness = new Harness();
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 2,
                Comedores =
                [
                    new ChatbotPublicComedorDto { Nombre = "Comedor A", Distrito = "Centro" },
                    new ChatbotPublicComedorDto { Nombre = "Comedor B", Distrito = "Norte" }
                ],
                Distritos = ["Centro", "Norte"]
            });

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "¿Qué comedores están registrados?" });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SuficienteInformacion);
        Assert.Contains("Comedor A (Centro)", result.Value.Respuesta, StringComparison.Ordinal);
        Assert.Contains("Comedor B (Norte)", result.Value.Respuesta, StringComparison.Ordinal);
        Assert.Equal("directorio-publico-comedores", Assert.Single(result.Value.Fuentes).Id);
        await harness.Retriever.DidNotReceive().RetrieveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await harness.Gemini.DidNotReceive().GenerateEmbeddingAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await harness.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_ConsultaPublicaSinComedores_DevuelveCeroConFuente()
    {
        using var harness = new Harness();
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto());

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "¿Cuántos comedores hay?" });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SuficienteInformacion);
        Assert.Equal("Actualmente no hay comedores registrados.", result.Value.Respuesta);
        Assert.Single(result.Value.Fuentes);
    }

    [Fact]
    public async Task Preguntar_PreguntaPorDistritos_UsaDistritosDelProveedor()
    {
        using var harness = new Harness();
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 2,
                Distritos = ["Centro", "Norte"]
            });

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "¿En qué distritos hay comedores?" });

        Assert.True(result.IsSuccess);
        Assert.Contains("Centro, Norte", result.Value.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_MasDeVeinteComedores_IndicaQueLaListaFueTruncada()
    {
        using var harness = new Harness();
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 21,
                Comedores = Enumerable.Range(1, 20)
                    .Select(index => new ChatbotPublicComedorDto
                    {
                        Nombre = $"Comedor {index:00}",
                        Distrito = "Centro"
                    })
                    .ToArray()
            });

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "¿Qué comedores están registrados?" });

        Assert.True(result.IsSuccess);
        Assert.Contains("Se muestran 20 de 21", result.Value.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_ConsultaPublicaCompatibleConContratoActual()
    {
        using var harness = new Harness();
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto { TotalComedores = 1 });

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "¿Cuántos comedores hay?" });
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"respuesta\"", json, StringComparison.Ordinal);
        Assert.Contains("\"suficienteInformacion\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fuentes\"", json, StringComparison.Ordinal);
        Assert.Contains("directorio-publico-comedores", json, StringComparison.Ordinal);
        Assert.DoesNotContain("telefono", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stock", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("direccion", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preguntar_ConsultaHibrida_CombinaAmbasFuentes()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment(content: "La guía pública explica el proceso de donación."));
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 1,
                Comedores = [new ChatbotPublicComedorDto { Nombre = "Comedor A", Distrito = "Centro" }],
                Distritos = ["Centro"]
            });
        harness.SetupGeneration("La guía explica el proceso de donación [S1]. El directorio registra un comedor [D1].");

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto
            {
                Pregunta = "¿Qué comedores están disponibles y cómo puedo donar alimentos?"
            });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SuficienteInformacion);
        Assert.Equal(2, result.Value.Fuentes.Count);
        Assert.Equal(new[] { "S1", "D1" }, result.Value.Fuentes.Select(source => source.Id));
        await harness.Retriever.Received(1).RetrieveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await harness.PublicData.Received(1).ObtenerDirectorioComedoresAsync(
            Arg.Any<CancellationToken>());
        await harness.Gemini.Received(1).GenerateAsync(
            Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_Hibrida_EnvíaContextosSeparadosYSoloCamposPublicos()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment(content: "La guía institucional describe cómo donar."));
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 1,
                Comedores = [new ChatbotPublicComedorDto { Nombre = "Comedor <A>", Distrito = "Centro" }],
                Distritos = ["Centro"]
            });
        harness.SetupGeneration("La guía describe la donación [S1]. El comedor figura en el directorio [D1].");

        var result = await harness.Service.PreguntarAsync(new PreguntaChatbotDto
        {
            Pregunta = "¿Cómo puedo donar alimentos y qué comedores están registrados?"
        });

        Assert.True(result.IsSuccess);
        Assert.Contains("Comedor &lt;A&gt;", GetGenerationRequest(harness).UserPrompt, StringComparison.Ordinal);
        Assert.Contains("<documentos>", GetGenerationRequest(harness).UserPrompt, StringComparison.Ordinal);
        Assert.Contains("<datos_publicos>", GetGenerationRequest(harness).UserPrompt, StringComparison.Ordinal);
        Assert.Contains("D1", GetGenerationRequest(harness).UserPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("telefono", GetGenerationRequest(harness).UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stock", GetGenerationRequest(harness).UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kardex", GetGenerationRequest(harness).UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("direccion", GetGenerationRequest(harness).UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dato no confiable", GetGenerationRequest(harness).SystemInstruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preguntar_Hibrida_RechazaReferenciaDinamicaInventada()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        SetupHybridData(harness);
        harness.SetupGeneration("La guía explica el proceso [S1]. Hay comedores registrados [D99].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
        Assert.Empty(result.Value.Fuentes);
    }

    [Fact]
    public async Task Preguntar_HibridaSinCitas_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        SetupHybridData(harness);
        harness.SetupGeneration("La guía explica el proceso y hay un comedor.");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
        Assert.Contains(
            "NoCitations",
            string.Join(" ", harness.Logger.Messages),
            StringComparison.Ordinal);
        Assert.DoesNotContain("La guía explica el proceso y hay un comedor", string.Join(" ", harness.Logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_HibridaSinDocumento_RespondeConDatoYMarcaParcial()
    {
        using var harness = new Harness();
        harness.Retriever.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<DocumentSearchResult>>(
                new RagRetrievalException(RagRetrievalFailure.QdrantUnavailable, "no disponible")));
        SetupHybridData(harness);
        harness.SetupGeneration("El directorio registra un comedor [D1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Contains("respuesta es parcial", result.Value.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("D1", Assert.Single(result.Value.Fuentes).Id);
        Assert.Contains("fuente_no_disponible", GetGenerationRequest(harness).UserPrompt, StringComparison.Ordinal);
        await harness.Gemini.Received(1).GenerateAsync(
            Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_HibridaSinDocumentosRelevantes_RespondeConDatosPublicosComoParcial()
    {
        using var harness = new Harness();
        harness.SetupRetriever();
        SetupHybridData(harness);
        harness.SetupGeneration("El directorio registra un comedor [D1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal("D1", Assert.Single(result.Value.Fuentes).Id);
        Assert.Contains("sin_documentos_relevantes", GetGenerationRequest(harness).UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_HibridaSinDirectorio_RespondeConDocumentoYMarcaParcial()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ChatbotDynamicDataDto>>(_ =>
                throw new InvalidOperationException("error-interno-no-publicable"));
        harness.SetupGeneration("La documentación explica el proceso [S1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Contains("respuesta es parcial", result.Value.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("S1", Assert.Single(result.Value.Fuentes).Id);
        Assert.DoesNotContain("error-interno-no-publicable", result.Value.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("error-interno-no-publicable", string.Join(" ", harness.Logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_HibridaAmbasFuentesFallan_NoGeneraNiExponeErrorInterno()
    {
        using var harness = new Harness();
        harness.Retriever.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<DocumentSearchResult>>(
                new RagRetrievalException(RagRetrievalFailure.QdrantUnavailable, "detalle")));
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ChatbotDynamicDataDto>>(_ =>
                throw new InvalidOperationException("detalle-db-privado"));

        var result = await AskHybrid(harness);

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.QdrantNoDisponible, result.ErrorCode);
        await harness.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain("detalle-db-privado", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_HibridaCeroComedores_CitaDirectorioYMantieneCeroReal()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto());
        harness.SetupGeneration("El directorio indica que no hay comedores registrados [D1]. La guía de donación está disponible [S1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.Contains("D1", string.Join(" ", result.Value.Fuentes.Select(source => source.Id)), StringComparison.Ordinal);
        Assert.Contains("S1", string.Join(" ", result.Value.Fuentes.Select(source => source.Id)), StringComparison.Ordinal);
        Assert.Equal(2, result.Value.Fuentes.Count);
    }

    [Fact]
    public async Task Preguntar_HibridaLimitaComedoresDelPromptAVeinteYConservaTotal()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 25,
                Comedores = Enumerable.Range(1, 25)
                    .Select(index => new ChatbotPublicComedorDto
                    {
                        Nombre = $"Comedor {index:00}",
                        Distrito = "Centro"
                    })
                    .ToArray(),
                Distritos = ["Centro"]
            });
        harness.SetupGeneration("La guía explica la donación [S1]. El directorio muestra hasta veinte nombres [D1].");

        var result = await AskHybrid(harness);
        var prompt = GetGenerationRequest(harness).UserPrompt;

        Assert.True(result.IsSuccess);
        Assert.Contains("total_registrado_visible=25", prompt, StringComparison.Ordinal);
        Assert.Contains("Comedor 20", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Comedor 21", prompt, StringComparison.Ordinal);
        Assert.Contains("listado_limitado=true", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_HibridaTimeoutDeQdrant_ConservaDatoPublicoComoParcial()
    {
        using var harness = new Harness();
        harness.Retriever.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<DocumentSearchResult>>(
                new RagRetrievalException(RagRetrievalFailure.QdrantTimeout, "timeout")));
        SetupHybridData(harness);
        harness.SetupGeneration("El directorio contiene un comedor [D1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal("D1", Assert.Single(result.Value.Fuentes).Id);
    }

    [Fact]
    public async Task Preguntar_HibridaCancelada_PropagaCancelacionYNoGenera()
    {
        using var harness = new Harness();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        harness.Retriever.RetrieveAsync(Arg.Any<string>(), cancellation.Token)
            .Returns<Task<IReadOnlyList<DocumentSearchResult>>>(_ =>
                throw new OperationCanceledException(cancellation.Token));
        harness.PublicData.ObtenerDirectorioComedoresAsync(cancellation.Token)
            .Returns<Task<ChatbotDynamicDataDto>>(_ =>
                throw new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AskHybrid(harness, cancellation.Token));
        await harness.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_HibridaTimeoutDeGemini_DevuelveErrorControlado()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        SetupHybridData(harness);
        harness.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiGenerationResult>>(_ =>
                throw new AiProviderException(AiFailureKind.Timeout, "timeout"));

        var result = await AskHybrid(harness);

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.AiTimeout, result.ErrorCode);
    }

    [Fact]
    public async Task Preguntar_HibridaGeminiNoDisponible_DevuelveErrorControlado()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        SetupHybridData(harness);
        harness.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AiGenerationResult>>(_ =>
                throw new AiProviderException(AiFailureKind.UpstreamError, "detalle-interno"));

        var result = await AskHybrid(harness);

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.AiNoDisponible, result.ErrorCode);
        Assert.DoesNotContain("detalle-interno", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_HibridaTimeoutDelDirectorio_RespondeConDocumentoParcial()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ChatbotDynamicDataDto>>(_ =>
                throw new OperationCanceledException("timeout-interno"));
        harness.SetupGeneration("La documentación explica el proceso [S1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal("S1", Assert.Single(result.Value.Fuentes).Id);
        Assert.Contains("respuesta es parcial", result.Value.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preguntar_HibridaCitaDocumentalInexistente_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        SetupHybridData(harness);
        harness.SetupGeneration("La guía explica el proceso [S9]. El directorio registra un comedor [D1].");

        var result = await AskHybrid(harness);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_HibridaMantieneContratoJsonExistente()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        SetupHybridData(harness);
        harness.SetupGeneration("La guía explica el proceso [S1]. El directorio registra un comedor [D1].");

        var result = await AskHybrid(harness);
        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"respuesta\"", json, StringComparison.Ordinal);
        Assert.Contains("\"suficienteInformacion\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fuentes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"S1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"D1\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_DatosPrivadosEnPreguntaMixta_NoConsultaProveedorPublico()
    {
        using var harness = new Harness();
        harness.SetupRetriever();

        var result = await harness.Service.PreguntarAsync(new PreguntaChatbotDto
        {
            Pregunta = "¿Cómo puedo donar alimentos y qué comedores están registrados, además de su stock?"
        });

        Assert.True(result.IsSuccess);
        await harness.PublicData.DidNotReceive().ObtenerDirectorioComedoresAsync(
            Arg.Any<CancellationToken>());
        await harness.Retriever.Received(1).RetrieveAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_ErrorDelProveedorPublico_NoFiltraDetalleInterno()
    {
        using var harness = new Harness();
        harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ChatbotDynamicDataDto>>(_ =>
                throw new InvalidOperationException("detalle-interno-de-prueba"));

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "¿Cuántos comedores hay?" });

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.ErrorInterno, result.ErrorCode);
        Assert.DoesNotContain("detalle-interno-de-prueba", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("detalle-interno-de-prueba", string.Join(" ", harness.Logger.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_CancelacionEnConsultaPublica_SePropaga()
    {
        using var harness = new Harness();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        harness.PublicData.ObtenerDirectorioComedoresAsync(cancellation.Token)
            .Returns<Task<ChatbotDynamicDataDto>>(_ =>
                throw new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Service.PreguntarAsync(
                new PreguntaChatbotDto { Pregunta = "¿Cuántos comedores hay?" },
                cancellation.Token));
    }

    [Fact]
    public async Task Preguntar_PreguntaInvalida_DevuelveErrorYNoRecupera()
    {
        using var harness = new Harness();

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = "ab" });

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.PreguntaInvalida, result.ErrorCode);
        await harness.Retriever.DidNotReceive().RetrieveAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await harness.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_SinFragmentos_DevuelveInsuficienteSinLlamarAGemini()
    {
        using var harness = new Harness();
        harness.SetupRetriever();

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.InsufficientInformationMessage, result.Value.Respuesta);
        await harness.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(),
            Arg.Any<CancellationToken>());
        await harness.PublicData.DidNotReceive().ObtenerDirectorioComedoresAsync(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_ContextoVacioTrasDeduplicar_DevuelveInsuficiente()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment(content: "   "));

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        await harness.Gemini.DidNotReceive().GenerateAsync(
            Arg.Any<AiGenerationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Preguntar_RespuestaConCitaValida_DevuelveSoloFuenteCitada()
    {
        using var harness = new Harness();
        harness.SetupRetriever(
            TestSupport.Fragment(
                title: "Cómo donar",
                source: "donaciones",
                content: "Para donar alimentos debes registrar la donación con cantidad e insumo.",
                score: 0.91),
            TestSupport.Fragment(
                title: "Comedores",
                source: "comedores",
                content: "Los comedores informan su capacidad diaria y beneficiarios atendidos.",
                score: 0.75));
        harness.SetupGeneration("Debes registrar la donación con cantidad e insumo [S1].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SuficienteInformacion);
        var source = Assert.Single(result.Value.Fuentes);
        Assert.Equal("S1", source.Id);
        Assert.Equal("Cómo donar", source.Titulo);
        Assert.Equal("donaciones", source.Fuente);
        Assert.Equal(0.91, source.Relevancia);
    }

    [Fact]
    public async Task Preguntar_RespuestaCitaSoloLaSegundaFuente_NoDevuelveLaPrimera()
    {
        using var harness = new Harness();
        harness.SetupRetriever(
            TestSupport.Fragment(
                title: "Donaciones",
                source: "donaciones",
                content: "El registro de donaciones exige cantidad, insumo y fecha de recepción.",
                score: 0.92),
            TestSupport.Fragment(
                title: "Privacidad",
                source: "privacidad",
                content: "Los datos personales de beneficiarios no se publican en el portal.",
                score: 0.81));
        harness.SetupGeneration("Los datos personales no se publican [S2].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        var source = Assert.Single(result.Value.Fuentes);
        Assert.Equal("S2", source.Id);
        Assert.Equal("privacidad", source.Fuente);
    }

    [Fact]
    public async Task Preguntar_CitaFueraDelContexto_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("Una respuesta inventada [S9].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_CitasAgrupadas_DevuelveTodasLasFuentesCitadasEnOrden()
    {
        using var harness = new Harness();
        harness.SetupRetriever(
            TestSupport.Fragment(
                source: "donaciones",
                score: 0.93,
                content: "El registro de donaciones exige cantidad, insumo y fecha de recepción."),
            TestSupport.Fragment(
                source: "comedores",
                score: 0.84,
                content: "Los comedores informan su capacidad diaria y beneficiarios atendidos."),
            TestSupport.Fragment(
                source: "privacidad",
                score: 0.77,
                content: "Los datos personales de beneficiarios no se publican en el portal."));
        harness.SetupGeneration("La donación se registra con cantidad e insumo [S1, S3].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SuficienteInformacion);
        Assert.Equal(2, result.Value.Fuentes.Count);
        Assert.Equal("S1", result.Value.Fuentes[0].Id);
        Assert.Equal("S3", result.Value.Fuentes[1].Id);
    }

    [Fact]
    public async Task Preguntar_CitaAgrupadaFueraDelContexto_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("Una respuesta con una fuente inventada [S1, S9].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_CitaConFormatoInvalido_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("Una respuesta con formato raro [S1, documento].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_RespuestaSinCitas_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("Una respuesta sin ninguna cita.");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_SinExigirCitas_DevuelveTodasLasFuentesDelContexto()
    {
        var options = new AiRagOptions { RequireCitations = false };
        using var harness = new Harness(options);
        harness.SetupRetriever(
            TestSupport.Fragment(
                source: "uno",
                score: 0.91,
                content: "El registro de donaciones exige cantidad, insumo y fecha de recepción."),
            TestSupport.Fragment(
                source: "dos",
                score: 0.81,
                content: "Los comedores informan su capacidad diaria y beneficiarios atendidos."));
        harness.SetupGeneration("Respuesta sin citas.");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.SuficienteInformacion);
        Assert.Equal(2, result.Value.Fuentes.Count);
    }

    [Fact]
    public async Task Preguntar_GeminiIndicaInformacionInsuficiente_DevuelveFuentesVacias()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration(RagAnswerService.InsufficientInformationMessage);

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.InsufficientInformationMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_ContenidoBloqueado_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiGenerationResult>(
                new AiProviderException(AiFailureKind.Blocked, "bloqueado")));

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_ProveedorConTimeout_DevuelveErrorDeTimeout()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiGenerationResult>(
                new AiProviderException(AiFailureKind.Timeout, "timeout")));

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.AiTimeout, result.ErrorCode);
    }

    [Fact]
    public async Task Preguntar_RecuperacionFallida_DevuelveErrorControlado()
    {
        using var harness = new Harness();
        harness.Retriever.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<DocumentSearchResult>>(
                new RagRetrievalException(RagRetrievalFailure.QdrantTimeout, "timeout")));

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.QdrantTimeout, result.ErrorCode);
    }

    [Fact]
    public async Task Preguntar_RespuestaConSecretoConocido_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("La clave es [S1].");
        harness.Redactor.ContainsSecret(Arg.Any<string>()).Returns(true);

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Empty(result.Value.Fuentes);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_RespuestaConFugaDelPrompt_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("JERARQUÍA DE INSTRUCCIONES: las reglas son [S1].");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_RespuestaConHtmlInseguro_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("<script>alert(1)</script> [S1]");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_RespuestaVacia_DevuelveFallbackSeguro()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration("   ");

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.SuficienteInformacion);
        Assert.Equal(RagAnswerService.SafeFallbackMessage, result.Value.Respuesta);
    }

    [Fact]
    public async Task Preguntar_NoRegistraPreguntaNiRespuestaCompletas()
    {
        const string pregunta = "pregunta-confidencial-xyz-123";
        const string respuesta = "respuesta-confidencial-abc-456 [S1]";

        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.SetupGeneration(respuesta);

        var result = await harness.Service.PreguntarAsync(
            new PreguntaChatbotDto { Pregunta = pregunta });

        Assert.True(result.IsSuccess);

        var logs = string.Join("\n", harness.Logger.Messages);
        Assert.DoesNotContain(pregunta, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(respuesta, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("respuesta-confidencial", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preguntar_ProveedorNoConfigurado_DevuelveErrorControlado()
    {
        using var harness = new Harness();
        harness.SetupRetriever(TestSupport.Fragment());
        harness.Gemini.GenerateAsync(
                Arg.Any<AiGenerationRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiGenerationResult>(
                new AiProviderException(AiFailureKind.NotConfigured, "sin clave")));

        var result = await harness.Service.PreguntarAsync(ValidQuestion());

        Assert.True(result.IsFailure);
        Assert.Equal(ChatbotErrorCodes.AiNoConfigurada, result.ErrorCode);
    }

    private static void SetupHybridData(Harness harness)
        => harness.PublicData.ObtenerDirectorioComedoresAsync(Arg.Any<CancellationToken>())
            .Returns(new ChatbotDynamicDataDto
            {
                TotalComedores = 1,
                Comedores = [new ChatbotPublicComedorDto { Nombre = "Comedor A", Distrito = "Centro" }],
                Distritos = ["Centro"]
            });

    private static Task<Rescauta.Application.Common.Result<RespuestaChatbotDto>> AskHybrid(
        Harness harness,
        CancellationToken cancellationToken = default)
        => harness.Service.PreguntarAsync(new PreguntaChatbotDto
        {
            Pregunta = "¿Cómo puedo donar alimentos y qué comedores están registrados?"
        }, cancellationToken);

    private static AiGenerationRequest GetGenerationRequest(Harness harness)
    {
        Assert.NotNull(harness.LastGenerationRequest);
        return harness.LastGenerationRequest;
    }

    private static PreguntaChatbotDto ValidQuestion()
        => new() { Pregunta = "¿Cómo se registra una donación de alimentos?" };

    private sealed class Harness : IDisposable
    {
        private readonly RagConcurrencyGate _gate;

        public Harness(AiRagOptions? options = null)
        {
            Options = options ?? new AiRagOptions();
            var boundOptions = TestSupport.Options(Options);

            Redactor.ContainsSecret(Arg.Any<string>()).Returns(false);

            _gate = new RagConcurrencyGate(boundOptions);

            Service = new RagAnswerService(
                new PreguntaChatbotValidator(boundOptions),
                Retriever,
                new RagContextBuilder(boundOptions),
                new RagPromptBuilder(),
                Gemini,
                new ChatbotQuestionRouter(),
                PublicData,
                Redactor,
                _gate,
                boundOptions,
                Logger);
        }

        public AiRagOptions Options { get; }

        public IRagRetriever Retriever { get; } = Substitute.For<IRagRetriever>();

        public IGeminiClient Gemini { get; } = Substitute.For<IGeminiClient>();

        public IChatbotPublicDataProvider PublicData { get; } = Substitute.For<IChatbotPublicDataProvider>();

        public ISecretRedactor Redactor { get; } = Substitute.For<ISecretRedactor>();

        public CapturingLogger<RagAnswerService> Logger { get; } = new();

        public RagAnswerService Service { get; }

        public AiGenerationRequest? LastGenerationRequest { get; private set; }

        public void SetupRetriever(params DocumentSearchResult[] fragments)
        {
            Retriever.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<DocumentSearchResult>>(fragments));
        }

        public void SetupGeneration(string text, string finishReason = "STOP")
        {
            Gemini.GenerateAsync(
                    Arg.Any<AiGenerationRequest>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    LastGenerationRequest = callInfo.Arg<AiGenerationRequest>();
                    return Task.FromResult(new AiGenerationResult(
                        text,
                        finishReason,
                        PromptTokens: 100,
                        CandidateTokens: 50,
                        TotalTokens: 150));
                });
        }

        public void Dispose() => _gate.Dispose();
    }
}
