using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rescauta.Application.Features.Ai;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;
using Xunit;

namespace Rescauta.Tests;

public sealed class RagRetrieverTests
{
    private static readonly float[] Embedding = [0.1f, 0.2f, 0.3f];

    private static (RagRetriever Retriever, IGeminiClient Gemini, IQdrantClient Qdrant) Create(
        AiRagOptions? options = null)
    {
        var gemini = Substitute.For<IGeminiClient>();
        var qdrant = Substitute.For<IQdrantClient>();

        gemini.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<float>>(Embedding));

        qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocumentSearchResult>>(
                Array.Empty<DocumentSearchResult>()));

        var retriever = new RagRetriever(
            gemini,
            qdrant,
            TestSupport.Options(options ?? new AiRagOptions()),
            NullLogger<RagRetriever>.Instance);

        return (retriever, gemini, qdrant);
    }

    [Fact]
    public async Task Retrieve_GeneraEmbeddingDeLaPreguntaYBuscaEnQdrant()
    {
        var (retriever, gemini, qdrant) = Create();
        const string pregunta = "¿Cómo funciona una donación?";

        await retriever.RetrieveAsync(pregunta);

        await gemini.Received(1).GenerateEmbeddingAsync(
            pregunta,
            Arg.Any<CancellationToken>());

        await qdrant.Received(1).SearchAsync(
            Embedding,
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retrieve_FiltraPorRelevanciaYOrdenaDeterministicamente()
    {
        var (retriever, _, qdrant) = Create(new AiRagOptions { MinRelevanceScore = 0.35 });
        var scoreAlto = TestSupport.Fragment(source: "zeta", score: 0.93, content: "Contenido alto.");
        var scoreMedio = TestSupport.Fragment(source: "alta", score: 0.80, content: "Contenido medio.");
        var scoreBajo = TestSupport.Fragment(source: "beta", score: 0.10, content: "Contenido bajo.");

        qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocumentSearchResult>>(
                [scoreBajo, scoreMedio, scoreAlto]));

        var results = await retriever.RetrieveAsync("pregunta válida");

        Assert.Equal(2, results.Count);
        Assert.Equal(scoreAlto.Id, results[0].Id);
        Assert.Equal(scoreMedio.Id, results[1].Id);
    }

    [Fact]
    public async Task Retrieve_IgnoraFragmentosSinContenido()
    {
        var (retriever, _, qdrant) = Create();
        var vacio = TestSupport.Fragment(score: 0.99, content: "   ");
        var valido = TestSupport.Fragment(score: 0.80, content: "Contenido válido.");

        qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DocumentSearchResult>>([vacio, valido]));

        var results = await retriever.RetrieveAsync("pregunta válida");

        var result = Assert.Single(results);
        Assert.Equal(valido.Id, result.Id);
    }

    [Fact]
    public async Task Retrieve_NoRealizaEscriturasEnQdrant()
    {
        var (retriever, _, qdrant) = Create();

        await retriever.RetrieveAsync("pregunta válida");

        await qdrant.DidNotReceive().EnsureCollectionAsync(Arg.Any<CancellationToken>());
        await qdrant.DidNotReceive().UpsertPointsAsync(
            Arg.Any<IReadOnlyList<DocumentChunk>>(),
            Arg.Any<IReadOnlyList<IReadOnlyList<float>>>(),
            Arg.Any<CancellationToken>());
        await qdrant.DidNotReceive().DeletePointsBySourceAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<Guid>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retrieve_TimeoutDeEmbedding_SeClasificaComoAiTimeout()
    {
        var (retriever, gemini, _) = Create(new AiRagOptions { EmbeddingTimeoutSeconds = 1 });

        gemini.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.Arg<CancellationToken>()));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.AiTimeout, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_ErrorDeRedEnGemini_SeClasificaComoAiUnavailable()
    {
        var (retriever, gemini, _) = Create();

        gemini.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("sin red"));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.AiUnavailable, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_GeminiMalConfigurado_SeClasificaComoAiNotConfigured()
    {
        var (retriever, gemini, _) = Create();

        gemini.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("configuración ausente"));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.AiNotConfigured, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_EmbeddingInvalido_SeClasificaComoAiInvalidResponse()
    {
        var (retriever, gemini, _) = Create();

        gemini.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("vector inválido"));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.AiInvalidResponse, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_ErrorDeRedEnQdrant_SeClasificaComoQdrantUnavailable()
    {
        var (retriever, _, qdrant) = Create();

        qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("sin red"));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.QdrantUnavailable, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_RespuestaInvalidaDeQdrant_SeClasificaComoQdrantUnavailable()
    {
        var (retriever, _, qdrant) = Create();

        qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("json inválido"));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.QdrantUnavailable, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_TimeoutDeQdrant_SeClasificaComoQdrantTimeout()
    {
        var (retriever, _, qdrant) = Create(new AiRagOptions { QdrantTimeoutSeconds = 1 });

        qdrant.SearchAsync(
                Arg.Any<IReadOnlyList<float>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForQdrantCancellation(callInfo.Arg<CancellationToken>()));

        var exception = await Assert.ThrowsAsync<RagRetrievalException>(
            () => retriever.RetrieveAsync("pregunta válida"));

        Assert.Equal(RagRetrievalFailure.QdrantTimeout, exception.Failure);
    }

    [Fact]
    public async Task Retrieve_CancelacionDelCliente_PropagaLaExcepcionOriginal()
    {
        var (retriever, gemini, _) = Create(new AiRagOptions { EmbeddingTimeoutSeconds = 30 });
        using var cts = new CancellationTokenSource();

        gemini.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.Arg<CancellationToken>()));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => retriever.RetrieveAsync("pregunta válida", cts.Token));
    }

    [Fact]
    public async Task Retrieve_PreguntaVacia_EsRechazadaSinLlamarAlProveedor()
    {
        var (retriever, gemini, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => retriever.RetrieveAsync("  "));

        await gemini.DidNotReceive().GenerateEmbeddingAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static async Task<IReadOnlyList<float>> WaitForCancellation(
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return Embedding;
    }

    private static async Task<IReadOnlyList<DocumentSearchResult>> WaitForQdrantCancellation(
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return Array.Empty<DocumentSearchResult>();
    }
}
