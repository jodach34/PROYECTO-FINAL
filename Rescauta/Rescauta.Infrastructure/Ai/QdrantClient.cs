using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;
using Rescauta.Infrastructure.Options;

namespace Rescauta.Infrastructure.Ai;

/// <summary>
/// Cliente HTTP tipado de Qdrant. Usa la API REST oficial y envia la clave en la cabecera
/// <c>api-key</c>, que Qdrant Cloud espera. La clave nunca se registra ni se devuelve.
///
/// Responsabilidades de esta etapa:
///   * comprobar conectividad,
///   * crear la coleccion si no existe (dimension configurada + distancia Cosine),
///   * guardar fragmentos con sus embeddings y metadatos,
///   * buscar fragmentos por similitud semantica.
///
/// El chunking y la orquestacion del RAG no viven aqui: pertenecen a Application.
/// </summary>
public sealed class QdrantClient(
    HttpClient httpClient,
    IOptions<QdrantOptions> options,
    ILogger<QdrantClient> logger) : IQdrantClient
{
    private const string Service = "Qdrant";
    private const string ExpectedDistance = "Cosine";

    public string CollectionName => options.Value.Collection.Trim();

    public async Task<AiConnectivityResult> CheckConnectivityAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            return NotConfigured(
                "Qdrant__Url no esta configurada. Definirla en user-secrets en local o en la variable de entorno en Render.");
        }

        if (string.IsNullOrWhiteSpace(settings.Collection))
        {
            return NotConfigured("Qdrant__Collection es obligatoria.");
        }

        if (settings.VectorSize <= 0)
        {
            return NotConfigured("Qdrant__VectorSize debe ser mayor que cero.");
        }

        if (!TryBuildBaseUri(settings.Url, out var baseUri))
        {
            return NotConfigured("Qdrant__Url no es una URL HTTP/HTTPS valida.");
        }

        var collection = settings.Collection.Trim();
        var requestUri = BuildCollectionUri(baseUri, collection);

        try
        {
            using var request = CreateRequest(HttpMethod.Get, requestUri, settings.ApiKey);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new AiConnectivityResult(
                    Service,
                    IsConfigured: true,
                    IsReachable: true,
                    IsHealthy: false,
                    $"Qdrant responde, pero la coleccion '{collection}' no existe. Se puede crear con EnsureCollectionAsync.",
                    new[] { $"Coleccion: {collection}", $"Dimension esperada: {settings.VectorSize}" });
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning(
                    "Qdrant rechazo la comprobacion de conectividad con HTTP {StatusCode}.",
                    (int)response.StatusCode);

                return new AiConnectivityResult(
                    Service,
                    IsConfigured: true,
                    IsReachable: true,
                    IsHealthy: false,
                    "Qdrant rechazo las credenciales. Revisa Qdrant__ApiKey.",
                    new[] { $"Estado HTTP: {(int)response.StatusCode}" });
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadQdrantErrorAsync(response, cancellationToken);

                logger.LogWarning(
                    "Qdrant respondio HTTP {StatusCode}: {Error}",
                    (int)response.StatusCode,
                    error);

                return new AiConnectivityResult(
                    Service,
                    IsConfigured: true,
                    IsReachable: true,
                    IsHealthy: false,
                    $"Qdrant respondio HTTP {(int)response.StatusCode}.",
                    new[] { $"Respuesta: {error}" });
            }

            var vectorSize = await ReadVectorSizeAsync(response, cancellationToken);

            if (vectorSize is null)
            {
                return new AiConnectivityResult(
                    Service,
                    IsConfigured: true,
                    IsReachable: true,
                    IsHealthy: false,
                    $"Qdrant responde y la coleccion '{collection}' existe, pero no se pudo leer su dimension.",
                    new[] { $"Coleccion: {collection}" });
            }

            var dimensionMatches = vectorSize.Value == settings.VectorSize;

            var details = new List<string>
            {
                $"Coleccion: {collection}",
                $"Dimension de la coleccion: {vectorSize.Value}",
                $"Dimension configurada: {settings.VectorSize}"
            };

            return new AiConnectivityResult(
                Service,
                IsConfigured: true,
                IsReachable: true,
                IsHealthy: dimensionMatches,
                dimensionMatches
                    ? "Qdrant responde y la coleccion tiene la dimension esperada."
                    : $"La coleccion '{collection}' mide {vectorSize.Value}, pero Qdrant__VectorSize dice {settings.VectorSize}.",
                details);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Qdrant no respondio dentro del timeout configurado.");

            return Unreachable("Qdrant no respondio dentro del timeout configurado.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No se pudo conectar con Qdrant.");

            return Unreachable($"No se pudo conectar con Qdrant ({ex.HttpRequestError}).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado comprobando la conectividad con Qdrant.");

            return Unreachable($"Error inesperado comprobando Qdrant ({ex.GetType().Name}).");
        }
    }

    public async Task EnsureCollectionAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = GetSettings(out var baseUri);
        var collection = settings.Collection.Trim();
        var collectionUri = BuildCollectionUri(baseUri, collection);

        using var getRequest = CreateRequest(HttpMethod.Get, collectionUri, settings.ApiKey);

        using var getResponse = await httpClient.SendAsync(
            getRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (getResponse.StatusCode == HttpStatusCode.NotFound)
        {
            var createPayload = new
            {
                vectors = new
                {
                    size = settings.VectorSize,
                    distance = ExpectedDistance
                }
            };

            using var createRequest = CreateJsonRequest(
                HttpMethod.Put,
                collectionUri,
                settings.ApiKey,
                createPayload);

            using var createResponse = await httpClient.SendAsync(
                createRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Otra instancia pudo crearla entre el GET y el PUT. En ese caso se valida
            // la coleccion existente en vez de tratarlo como un fallo.
            if (createResponse.StatusCode == HttpStatusCode.Conflict)
            {
                await ValidateExistingCollectionAsync(
                    collectionUri,
                    settings,
                    cancellationToken);

                await EnsureSourceIndexAsync(
                    baseUri,
                    collection,
                    settings,
                    cancellationToken);

                return;
            }

            await EnsureSuccessAsync(
                createResponse,
                "crear la coleccion",
                collection,
                cancellationToken);

            await EnsureQdrantStatusOkAsync(
                createResponse,
                "crear la coleccion",
                collection,
                cancellationToken);

            logger.LogInformation(
                "Qdrant: coleccion {Collection} creada con dimension {VectorSize} y distancia {Distance}.",
                collection,
                settings.VectorSize,
                ExpectedDistance);

            await EnsureSourceIndexAsync(
                baseUri,
                collection,
                settings,
                cancellationToken);

            return;
        }

        await EnsureSuccessAsync(
            getResponse,
            "consultar la coleccion",
            collection,
            cancellationToken);

        var config = await ReadCollectionConfigAsync(
            getResponse,
            cancellationToken);

        ValidateCollectionConfig(
            config,
            collection,
            settings.VectorSize);

        await EnsureSourceIndexAsync(
            baseUri,
            collection,
            settings,
            cancellationToken);
    }

    public async Task UpsertPointsAsync(
        IReadOnlyList<DocumentChunk> documents,
        IReadOnlyList<IReadOnlyList<float>> embeddings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(embeddings);

        if (documents.Count != embeddings.Count)
        {
            throw new ArgumentException(
                "La cantidad de documentos debe coincidir con la cantidad de embeddings.",
                nameof(embeddings));
        }

        if (documents.Count == 0)
        {
            return;
        }

        var settings = GetSettings(out var baseUri);
        var collection = settings.Collection.Trim();
        var points = new List<object>(documents.Count);

        for (var index = 0; index < documents.Count; index++)
        {
            var document = documents[index];

            if (document is null)
            {
                throw new ArgumentException(
                    $"El documento en la posicion {index} es null.",
                    nameof(documents));
            }

            var embedding = embeddings[index];

            if (embedding is null)
            {
                throw new ArgumentException(
                    $"El embedding en la posicion {index} es null.",
                    nameof(embeddings));
            }

            if (document.Id == Guid.Empty)
            {
                throw new ArgumentException(
                    $"El documento en la posicion {index} no tiene un Id valido.",
                    nameof(documents));
            }

            if (string.IsNullOrWhiteSpace(document.Content))
            {
                throw new ArgumentException(
                    $"El documento en la posicion {index} no tiene contenido.",
                    nameof(documents));
            }

            if (embedding.Count != settings.VectorSize)
            {
                throw new ArgumentException(
                    $"El embedding en la posicion {index} tiene {embedding.Count} dimensiones; " +
                    $"se esperaban {settings.VectorSize}.",
                    nameof(embeddings));
            }

            points.Add(new Dictionary<string, object?>
            {
                ["id"] = document.Id.ToString(),
                ["vector"] = embedding.ToArray(),
                ["payload"] = new Dictionary<string, object?>
                {
                    ["title"] = document.Title,
                    ["content"] = document.Content,
                    ["source"] = document.Source,
                    ["category"] = document.Category,
                    ["metadata"] = document.Metadata is null
                        ? new Dictionary<string, string>()
                        : document.Metadata
                }
            });
        }

        var requestUri = new Uri(
            baseUri,
            $"collections/{Uri.EscapeDataString(collection)}/points?wait=true");

        using var request = CreateJsonRequest(
            HttpMethod.Put,
            requestUri,
            settings.ApiKey,
            new { points });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"La coleccion '{collection}' no existe. Ejecuta EnsureCollectionAsync antes de guardar puntos.");
        }

        await EnsureSuccessAsync(
            response,
            "guardar los puntos",
            collection,
            cancellationToken);

        await EnsureQdrantStatusOkAsync(
            response,
            "guardar los puntos",
            collection,
            cancellationToken);

        logger.LogInformation(
            "Qdrant: {Count} fragmentos guardados en la coleccion {Collection}.",
            documents.Count,
            collection);
    }

    public async Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        IReadOnlyList<float> embedding,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                "El limite debe ser mayor que cero.");
        }

        var settings = GetSettings(out var baseUri);

        if (embedding.Count != settings.VectorSize)
        {
            throw new ArgumentException(
                $"El embedding de busqueda tiene {embedding.Count} dimensiones; " +
                $"se esperaban {settings.VectorSize}.",
                nameof(embedding));
        }

        var collection = settings.Collection.Trim();
        var requestUri = new Uri(
            baseUri,
            $"collections/{Uri.EscapeDataString(collection)}/points/search");

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            requestUri,
            settings.ApiKey,
            new
            {
                vector = embedding.ToArray(),
                limit,
                with_payload = true,
                with_vector = false
            });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"La coleccion '{collection}' no existe. Ejecuta EnsureCollectionAsync antes de buscar.");
        }

        await EnsureSuccessAsync(
            response,
            "buscar puntos",
            collection,
            cancellationToken);

        return await ReadSearchResultsAsync(
            response,
            collection,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetPointIdsBySourceAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException(
                "La fuente es obligatoria para consultar sus puntos.",
                nameof(source));
        }

        var settings = GetSettings(out var baseUri);
        var collection = settings.Collection.Trim();
        var requestUri = new Uri(
            baseUri,
            $"collections/{Uri.EscapeDataString(collection)}/points/scroll");

        var ids = new List<Guid>();
        string? nextOffset = null;

        do
        {
            object payload = nextOffset is null
                ? new
                {
                    filter = BuildSourceFilter(source),
                    limit = 256,
                    with_payload = false,
                    with_vector = false
                }
                : new
                {
                    filter = BuildSourceFilter(source),
                    limit = 256,
                    offset = nextOffset,
                    with_payload = false,
                    with_vector = false
                };

            using var request = CreateJsonRequest(
                HttpMethod.Post,
                requestUri,
                settings.ApiKey,
                payload);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"La coleccion '{collection}' no existe. Ejecuta EnsureCollectionAsync antes de consultar puntos.");
            }

            await EnsureSuccessAsync(
                response,
                "consultar los puntos por fuente",
                collection,
                cancellationToken);

            var page = await ReadScrollPageAsync(
                response,
                collection,
                cancellationToken);

            ids.AddRange(page.Ids);
            nextOffset = page.NextOffset;
        }
        while (!string.IsNullOrWhiteSpace(nextOffset));

        return ids.Distinct().ToList();
    }

    public async Task DeletePointsBySourceAsync(
        string source,
        IReadOnlyCollection<Guid>? keepPointIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException(
                "La fuente es obligatoria para eliminar sus puntos.",
                nameof(source));
        }

        var settings = GetSettings(out var baseUri);
        var collection = settings.Collection.Trim();
        var keepIds = keepPointIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? new List<Guid>();

        var requestUri = new Uri(
            baseUri,
            $"collections/{Uri.EscapeDataString(collection)}/points/delete?wait=true");

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            requestUri,
            settings.ApiKey,
            new
            {
                filter = BuildSourceDeleteFilter(source, keepIds)
            });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"La coleccion '{collection}' no existe. Ejecuta EnsureCollectionAsync antes de eliminar puntos.");
        }

        await EnsureSuccessAsync(
            response,
            "eliminar los puntos obsoletos",
            collection,
            cancellationToken);

        await EnsureQdrantStatusOkAsync(
            response,
            "eliminar los puntos obsoletos",
            collection,
            cancellationToken);

        logger.LogInformation(
            "Qdrant: puntos obsoletos de la fuente {Source} eliminados en la coleccion {Collection} " +
            "(se conservaron {Kept} Ids).",
            source,
            collection,
            keepIds.Count);
    }

    private QdrantOptions GetSettings(out Uri baseUri)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            throw new InvalidOperationException(
                "Qdrant__Url no esta configurada.");
        }

        if (string.IsNullOrWhiteSpace(settings.Collection))
        {
            throw new InvalidOperationException(
                "Qdrant__Collection es obligatoria.");
        }

        if (settings.VectorSize <= 0)
        {
            throw new InvalidOperationException(
                "Qdrant__VectorSize debe ser mayor que cero.");
        }

        if (!TryBuildBaseUri(settings.Url, out baseUri))
        {
            throw new InvalidOperationException(
                "Qdrant__Url no es una URL HTTP/HTTPS valida.");
        }

        return settings;
    }

    private async Task ValidateExistingCollectionAsync(
        Uri collectionUri,
        QdrantOptions settings,
        CancellationToken cancellationToken)
    {
        var collection = settings.Collection.Trim();

        using var request = CreateRequest(HttpMethod.Get, collectionUri, settings.ApiKey);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(
            response,
            "consultar la coleccion",
            collection,
            cancellationToken);

        var config = await ReadCollectionConfigAsync(
            response,
            cancellationToken);

        ValidateCollectionConfig(
            config,
            collection,
            settings.VectorSize);
    }

    /// <summary>
    /// Garantiza el indice keyword del payload "source". Qdrant lo exige para filtrar,
    /// recorrer y borrar puntos por fuente; sin el, esas operaciones devuelven HTTP 400.
    /// Es idempotente: si el indice ya existe se ignora el conflicto.
    /// </summary>
    private async Task EnsureSourceIndexAsync(
        Uri baseUri,
        string collection,
        QdrantOptions settings,
        CancellationToken cancellationToken)
    {
        const string fieldName = "source";

        var requestUri = new Uri(
            baseUri,
            $"collections/{Uri.EscapeDataString(collection)}/index?wait=true");

        using var request = CreateJsonRequest(
            HttpMethod.Put,
            requestUri,
            settings.ApiKey,
            new
            {
                field_name = fieldName,
                field_schema = "keyword"
            });

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            await EnsureQdrantStatusOkAsync(
                response,
                "crear el indice de payload source",
                collection,
                cancellationToken);

            logger.LogDebug(
                "Qdrant: indice de payload '{Field}' garantizado en la coleccion {Collection}.",
                fieldName,
                collection);

            return;
        }

        // Qdrant responde 400 cuando el indice ya existe. Es el resultado esperado en
        // cada reindexacion, no un fallo: se compara el mensaje para no ocultar otros
        // errores reales (por ejemplo, permisos o una coleccion inexistente).
        var error = await ReadQdrantErrorAsync(response, cancellationToken);

        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new HttpRequestException(
            $"Qdrant no pudo crear el indice de payload '{fieldName}' en la coleccion " +
            $"'{collection}'. HTTP {(int)response.StatusCode}: {error}");
    }

    private static void ValidateCollectionConfig(
        CollectionConfig? config,
        string collection,
        int expectedVectorSize)
    {
        if (config is null)
        {
            throw new InvalidOperationException(
                $"Qdrant no devolvio la configuracion de la coleccion '{collection}'.");
        }

        if (config.Value.Size != expectedVectorSize)
        {
            throw new InvalidOperationException(
                $"La coleccion '{collection}' tiene dimension {config.Value.Size}, " +
                $"pero Qdrant__VectorSize es {expectedVectorSize}. " +
                "No se puede mezclar dimensiones en una misma coleccion.");
        }

        if (!string.Equals(
                config.Value.Distance,
                ExpectedDistance,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La coleccion '{collection}' usa distancia " +
                $"'{config.Value.Distance ?? "desconocida"}', pero se esperaba '{ExpectedDistance}'.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        string collection,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await ReadQdrantErrorAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException(
                "Qdrant rechazo las credenciales. Revisa Qdrant__ApiKey.");
        }

        throw new HttpRequestException(
            $"Qdrant no pudo {operation} en la coleccion '{collection}'. " +
            $"HTTP {(int)response.StatusCode}: {error}");
    }

    private static async Task EnsureQdrantStatusOkAsync(
        HttpResponseMessage response,
        string operation,
        string collection,
        CancellationToken cancellationToken)
    {
        JsonDocument document;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Qdrant devolvio una respuesta invalida al {operation} en la coleccion '{collection}'.",
                ex);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("status", out var status))
            {
                return;
            }

            if (status.ValueKind == JsonValueKind.String)
            {
                var value = status.GetString();

                if (!string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Qdrant devolvio estado '{value}' al {operation} en la coleccion '{collection}'.");
                }

                return;
            }

            if (status.ValueKind == JsonValueKind.Object &&
                status.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                throw new HttpRequestException(
                    $"Qdrant no pudo {operation} en la coleccion '{collection}': " +
                    Truncate(error.GetString(), 300));
            }
        }
    }

    private static object BuildSourceFilter(string source)
        => new
        {
            must = new[]
            {
                new
                {
                    key = "source",
                    match = new { value = source }
                }
            }
        };

    private static object BuildSourceDeleteFilter(
        string source,
        IReadOnlyCollection<Guid> keepIds)
    {
        var must = new[]
        {
            new
            {
                key = "source",
                match = new { value = source }
            }
        };

        if (keepIds.Count == 0)
        {
            return new { must };
        }

        return new
        {
            must,
            must_not = new[]
            {
                new
                {
                    has_id = keepIds.Select(id => id.ToString()).ToArray()
                }
            }
        };
    }

    private static async Task<ScrollPage> ReadScrollPageAsync(
        HttpResponseMessage response,
        string collection,
        CancellationToken cancellationToken)
    {
        JsonDocument document;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Qdrant devolvio una respuesta invalida al consultar puntos en la coleccion '{collection}'.",
                ex);
        }

        using (document)
        {
            if (document.RootElement.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.String &&
                !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Qdrant devolvio estado '{status.GetString()}' al consultar puntos en la coleccion '{collection}'.");
            }

            if (!document.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Qdrant devolvio una respuesta sin resultados al consultar puntos en la coleccion '{collection}'.");
            }

            var ids = new List<Guid>();

            if (result.TryGetProperty("points", out var points) &&
                points.ValueKind == JsonValueKind.Array)
            {
                foreach (var point in points.EnumerateArray())
                {
                    if (!point.TryGetProperty("id", out var idElement) ||
                        idElement.ValueKind != JsonValueKind.String ||
                        !Guid.TryParse(idElement.GetString(), out var id))
                    {
                        throw new InvalidOperationException(
                            $"Qdrant devolvio un punto con Id invalido en la coleccion '{collection}'. " +
                            "La coleccion contiene Ids que no fueron generados por Rescauta.");
                    }

                    ids.Add(id);
                }
            }

            string? nextOffset = null;

            if (result.TryGetProperty("next_page_offset", out var nextElement) &&
                nextElement.ValueKind == JsonValueKind.String)
            {
                nextOffset = nextElement.GetString();
            }

            return new ScrollPage(ids, nextOffset);
        }
    }

    private static async Task<IReadOnlyList<DocumentSearchResult>> ReadSearchResultsAsync(
        HttpResponseMessage response,
        string collection,
        CancellationToken cancellationToken)
    {
        JsonDocument document;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Qdrant devolvio una respuesta invalida al buscar en la coleccion '{collection}'.",
                ex);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"Qdrant devolvio una respuesta sin resultados al buscar en la coleccion '{collection}'.");
            }

            var results = new List<DocumentSearchResult>();

            foreach (var point in result.EnumerateArray())
            {
                if (!point.TryGetProperty("id", out var idElement) ||
                    idElement.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(idElement.GetString(), out var id))
                {
                    throw new InvalidOperationException(
                        $"Qdrant devolvio un punto con Id invalido en la coleccion '{collection}'.");
                }

                if (!point.TryGetProperty("score", out var scoreElement) ||
                    scoreElement.ValueKind != JsonValueKind.Number)
                {
                    throw new InvalidOperationException(
                        $"Qdrant devolvio un punto sin score en la coleccion '{collection}'.");
                }

                var payload = point.TryGetProperty("payload", out var payloadElement) &&
                              payloadElement.ValueKind == JsonValueKind.Object
                    ? payloadElement
                    : default;

                results.Add(new DocumentSearchResult
                {
                    Id = id,
                    Title = ReadString(payload, "title"),
                    Content = ReadString(payload, "content"),
                    Source = ReadString(payload, "source"),
                    Category = ReadString(payload, "category"),
                    Score = scoreElement.GetDouble(),
                    Metadata = ReadMetadata(payload)
                });
            }

            return results;
        }
    }

    private static string ReadString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> ReadMetadata(JsonElement payload)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("metadata", out var metadataElement) ||
            metadataElement.ValueKind != JsonValueKind.Object)
        {
            return metadata;
        }

        foreach (var property in metadataElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                metadata[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return metadata;
    }

    private static AiConnectivityResult NotConfigured(string message)
        => new(
            Service,
            IsConfigured: false,
            IsReachable: false,
            IsHealthy: false,
            message,
            Array.Empty<string>());

    private static AiConnectivityResult Unreachable(string message)
        => new(
            Service,
            IsConfigured: true,
            IsReachable: false,
            IsHealthy: false,
            message,
            Array.Empty<string>());

    private static bool TryBuildBaseUri(string value, out Uri baseUri)
    {
        baseUri = null!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();

        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        baseUri = parsed;

        return true;
    }

    private static Uri BuildCollectionUri(Uri baseUri, string collection)
        => new(baseUri, $"collections/{Uri.EscapeDataString(collection)}");

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri requestUri,
        string apiKey)
    {
        var request = new HttpRequestMessage(method, requestUri);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        }

        return request;
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        Uri requestUri,
        string apiKey,
        object payload)
    {
        var request = CreateRequest(method, requestUri, apiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private static async Task<CollectionConfig?> ReadCollectionConfigAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        JsonDocument document;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Qdrant devolvio una respuesta invalida al leer la configuracion de la coleccion.",
                ex);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("config", out var config) ||
                !config.TryGetProperty("params", out var parameters) ||
                !parameters.TryGetProperty("vectors", out var vectors) ||
                vectors.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (vectors.TryGetProperty("size", out var directSize) &&
                directSize.TryGetInt32(out var size))
            {
                return new CollectionConfig(
                    size,
                    ReadDistance(vectors));
            }

            // Coleccion con vectores con nombre:
            //   { "texto": { "size": 3072, "distance": "Cosine" } }
            foreach (var vector in vectors.EnumerateObject())
            {
                if (vector.Value.ValueKind == JsonValueKind.Object &&
                    vector.Value.TryGetProperty("size", out var namedSize) &&
                    namedSize.TryGetInt32(out var namedVectorSize))
                {
                    return new CollectionConfig(
                        namedVectorSize,
                        ReadDistance(vector.Value));
                }
            }

            return null;
        }
    }

    private static string? ReadDistance(JsonElement vectorConfig)
    {
        if (vectorConfig.TryGetProperty("distance", out var distance) &&
            distance.ValueKind == JsonValueKind.String)
        {
            return distance.GetString();
        }

        return null;
    }

    private static async Task<int?> ReadVectorSizeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("config", out var config) ||
            !config.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("vectors", out var vectors))
        {
            return null;
        }

        // Qdrant devuelve dos formas segun la coleccion use vectores con nombre o no:
        //   sin nombre:  { "size": 3072, "distance": "Cosine" }
        //   con nombre:  { "texto": { "size": 3072, "distance": "Cosine" } }
        if (vectors.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (vectors.TryGetProperty("size", out var directSize) &&
            directSize.TryGetInt32(out var size))
        {
            return size;
        }

        foreach (var vector in vectors.EnumerateObject())
        {
            if (vector.Value.ValueKind == JsonValueKind.Object &&
                vector.Value.TryGetProperty("size", out var namedSize) &&
                namedSize.TryGetInt32(out var namedVectorSize))
            {
                return namedVectorSize;
            }
        }

        return null;
    }

    private static async Task<string> ReadQdrantErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("status", out var status) &&
                status.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return Truncate(error.GetString(), 300);
            }

            return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception)
        {
            return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "sin detalle";
        }

        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    private readonly record struct CollectionConfig(int Size, string? Distance);

    private readonly record struct ScrollPage(
        IReadOnlyList<Guid> Ids,
        string? NextOffset);
}
