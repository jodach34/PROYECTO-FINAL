using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rescauta.Api.Security;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Api.Controllers.v1;

/// <summary>
/// Operaciones administrativas de las dependencias de IA del chatbot.
///
///   GET  /api/v1/ai/connectivity -> comprueba Gemini y Qdrant sin devolver credenciales.
///   POST /api/v1/ai/index        -> prepara, fragmenta e indexa la documentación pública.
///
/// Requiere rol Administrador: ambas operaciones hacen llamadas externas con la ApiKey
/// del servidor y la indexación consume cuota de embeddings. La indexación NO se ejecuta
/// automáticamente al arrancar la API.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = AuthPolicies.Admin)]
public sealed class AiController(
    IGeminiClient geminiClient,
    IQdrantClient qdrantClient,
    IDocumentIndexer documentIndexer) : ApiControllerBase
{
    /// <summary>Comprueba que Gemini y Qdrant responden con la configuración actual.</summary>
    [HttpGet("connectivity")]
    [ProducesResponseType<AiConnectivityReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiConnectivityReport>> GetConnectivity(
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            geminiClient.CheckConnectivityAsync(cancellationToken),
            qdrantClient.CheckConnectivityAsync(cancellationToken));

        var report = new AiConnectivityReport(
            AllHealthy: results.All(result => result.IsHealthy),
            Gemini: results[0],
            Qdrant: results[1]);

        return Ok(report);
    }

    /// <summary>
    /// Indexa los documentos institucionales curados en la colección configurada.
    /// Devuelve el detalle de qué documentos se indexaron, cuáles no cambiaron y cuáles
    /// conservaron su versión anterior por un fallo.
    /// </summary>
    [HttpPost("index")]
    [ProducesResponseType<IndexingReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IndexingReport>> Index(
        CancellationToken cancellationToken)
    {
        var report = await documentIndexer.IndexAsync(cancellationToken);

        return Ok(report);
    }
}
