
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Interfaces.Ai;
using Rescauta.Infrastructure.Options;

namespace Rescauta.Infrastructure.Ai;

/// <summary>
/// Cliente HTTP tipado de Gemini.
/// Gestiona la conectividad y la generación de embeddings.
/// La clave API se envía mediante la cabecera x-goog-api-key.
/// </summary>
public sealed class GeminiClient(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<GeminiClient> logger) : IGeminiClient
{
    private const string Service = "Gemini";
    private const string ModelsRelativePath = "v1beta/models?pageSize=1000";

    public async Task<AiConnectivityResult> CheckConnectivityAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return NotConfigured(
                "Gemini__ApiKey no esta configurada. Definirla en user-secrets o en Render.");
        }

        if (string.IsNullOrWhiteSpace(settings.GenerationModel) ||
            string.IsNullOrWhiteSpace(settings.EmbeddingModel))
        {
            return NotConfigured(
                "Gemini__GenerationModel y Gemini__EmbeddingModel son obligatorios.");
        }

        if (!TryBuildBaseUri(settings.BaseUrl, out var baseUri))
        {
            return NotConfigured(
                "Gemini__BaseUrl no es una URL HTTP/HTTPS valida.");
        }

        var requestUri = new Uri(baseUri, ModelsRelativePath);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.TryAddWithoutValidation("x-goog-api-key", settings.ApiKey);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadGeminiErrorAsync(response, cancellationToken);

                logger.LogWarning(
                    "Gemini rechazo la comprobacion de conectividad con HTTP {StatusCode}: {Error}",
                    (int)response.StatusCode,
                    error);

                var message = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Gemini rechazo las credenciales. Revisa Gemini__ApiKey."
                    : $"Gemini respondio HTTP {(int)response.StatusCode}.";

                return new AiConnectivityResult(
                    Service,
                    IsConfigured: true,
                    IsReachable: true,
                    IsHealthy: false,
                    message,
                    new[]
                    {
                        $"Estado HTTP: {(int)response.StatusCode}",
                        $"Respuesta: {error}"
                    });
            }

            var availableModels = await ReadModelNamesAsync(response, cancellationToken);

            if (availableModels.Count == 0)
            {
                return new AiConnectivityResult(
                    Service,
                    IsConfigured: true,
                    IsReachable: true,
                    IsHealthy: false,
                    "Gemini respondio, pero la lista de modelos vino vacia.",
                    Array.Empty<string>());
            }

            var generationModel = NormalizeModelName(settings.GenerationModel);
            var embeddingModel = NormalizeModelName(settings.EmbeddingModel);

            var hasGeneration = availableModels.Contains(
                generationModel,
                StringComparer.OrdinalIgnoreCase);

            var hasEmbedding = availableModels.Contains(
                embeddingModel,
                StringComparer.OrdinalIgnoreCase);

            var isHealthy = hasGeneration && hasEmbedding;

            var details = new List<string>
            {
                $"Modelo de generacion '{generationModel}': {(hasGeneration ? "disponible" : "NO disponible")}",
                $"Modelo de embeddings '{embeddingModel}': {(hasEmbedding ? "disponible" : "NO disponible")}",
                $"Modelos visibles: {availableModels.Count}"
            };

            return new AiConnectivityResult(
                Service,
                IsConfigured: true,
                IsReachable: true,
                IsHealthy: isHealthy,
                isHealthy
                    ? "Gemini responde y los dos modelos configurados estan disponibles."
                    : "Gemini responde, pero alguno de los modelos configurados no aparece en la cuenta.",
                details);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Gemini no respondio dentro del timeout configurado.");

            return Unreachable(
                "Gemini no respondio dentro del timeout configurado.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No se pudo conectar con Gemini.");

            return Unreachable(
                $"No se pudo conectar con Gemini ({ex.HttpRequestError}).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado comprobando la conectividad con Gemini.");

            return Unreachable(
                $"Error inesperado comprobando Gemini ({ex.GetType().Name}).");
        }
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "El texto no puede estar vacio.",
                nameof(text));
        }

        var settings = options.Value;
        var vectorSize = qdrantOptions.Value.VectorSize;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini__ApiKey no esta configurada.");
        }

        if (!TryBuildBaseUri(settings.BaseUrl, out var baseUri))
        {
            throw new InvalidOperationException(
                "Gemini__BaseUrl no es una URL HTTP/HTTPS valida.");
        }

        if (vectorSize <= 0)
        {
            throw new InvalidOperationException(
                "Qdrant__VectorSize debe ser mayor que cero.");
        }

        var model = NormalizeModelName(settings.EmbeddingModel);
        var endpoint = $"v1beta/models/{model}:embedContent";
        var requestUri = new Uri(baseUri, endpoint);

        var payload = new
        {
            model = $"models/{model}",
            content = new
            {
                parts = new[]
                {
                    new { text }
                }
            },
            outputDimensionality = vectorSize
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            requestUri);

        request.Headers.TryAddWithoutValidation(
            "x-goog-api-key",
            settings.ApiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadGeminiErrorAsync(
                response,
                cancellationToken);

            logger.LogWarning(
                "Gemini fallo al generar un embedding. HTTP {StatusCode}: {Error}",
                (int)response.StatusCode,
                error);

            throw new HttpRequestException(
                $"Gemini no pudo generar el embedding. HTTP {(int)response.StatusCode}: {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("embedding", out var embedding) ||
            !embedding.TryGetProperty("values", out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Gemini devolvio una respuesta sin valores de embedding.");
        }

        var vector = values.EnumerateArray()
            .Select(value => value.GetSingle())
            .ToArray();

        if (vector.Length != vectorSize)
        {
            throw new InvalidOperationException(
                $"Dimension incorrecta: se esperaban {vectorSize} valores y llegaron {vector.Length}.");
        }

        return vector;
    }

    public async Task<AiGenerationResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SystemInstruction) ||
            string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            throw new ArgumentException(
                "La instrucción de sistema y el prompt de usuario son obligatorios.",
                nameof(request));
        }

        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new AiProviderException(
                AiFailureKind.NotConfigured,
                "Gemini no está configurado.");
        }

        if (!TryBuildBaseUri(settings.BaseUrl, out var baseUri))
        {
            throw new AiProviderException(
                AiFailureKind.NotConfigured,
                "La URL de Gemini no es válida.");
        }

        var model = NormalizeModelName(settings.GenerationModel);

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new AiProviderException(
                AiFailureKind.NotConfigured,
                "El modelo de generación no está configurado.");
        }

        var requestUri = new Uri(
            baseUri,
            $"v1beta/models/{model}:generateContent");

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = request.SystemInstruction }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = request.UserPrompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxOutputTokens,
                candidateCount = 1
            }
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            requestUri);

        httpRequest.Headers.TryAddWithoutValidation(
            "x-goog-api-key",
            settings.ApiKey);

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Gemini no respondió dentro del timeout configurado.");

            throw new AiProviderException(
                AiFailureKind.Timeout,
                "Gemini no respondió dentro del tiempo configurado.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                "No se pudo conectar con Gemini: {Error}",
                ex.HttpRequestError);

            throw new AiProviderException(
                AiFailureKind.Network,
                "No se pudo conectar con Gemini.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;

                var failureKind = statusCode switch
                {
                    429 => AiFailureKind.QuotaExceeded,
                    401 or 403 or 404 => AiFailureKind.NotConfigured,
                    400 => AiFailureKind.UpstreamError,
                    >= 500 => AiFailureKind.UpstreamError,
                    _ => AiFailureKind.UpstreamError
                };

                // No se lee ni se registra el cuerpo del error: solo el código HTTP.
                logger.LogWarning(
                    "Gemini falló al generar contenido. HTTP {StatusCode}.",
                    (int)response.StatusCode);

                throw new AiProviderException(
                    failureKind,
                    "Gemini no pudo generar la respuesta.");
            }

            JsonDocument document;

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(
                    cancellationToken);

                document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Gemini no respondió dentro del timeout configurado.");

                throw new AiProviderException(
                    AiFailureKind.Timeout,
                    "Gemini no respondió dentro del tiempo configurado.",
                    ex);
            }
            catch (JsonException ex)
            {
                logger.LogError("Gemini devolvió una respuesta JSON inválida.");

                throw new AiProviderException(
                    AiFailureKind.InvalidResponse,
                    "Gemini devolvió una respuesta inválida.",
                    ex);
            }

            using (document)
            {
                return ReadGenerationResult(document.RootElement);
            }
        }
    }

    private static AiGenerationResult ReadGenerationResult(JsonElement root)
    {
        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.ValueKind == JsonValueKind.Object &&
            feedback.TryGetProperty("blockReason", out var blockReason) &&
            blockReason.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(blockReason.GetString()))
        {
            throw new AiProviderException(
                AiFailureKind.Blocked,
                "El proveedor bloqueó la respuesta.");
        }

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            throw new AiProviderException(
                AiFailureKind.InvalidResponse,
                "El proveedor no devolvió candidatos.");
        }

        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var finish) &&
                           finish.ValueKind == JsonValueKind.String
            ? finish.GetString() ?? string.Empty
            : string.Empty;

        var text = string.Empty;

        if (candidate.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Object &&
            content.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var fragments = new List<string>();

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var partText) &&
                    partText.ValueKind == JsonValueKind.String)
                {
                    var value = partText.GetString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        fragments.Add(value);
                    }
                }
            }

            text = string.Join("\n", fragments).Trim();
        }

        if (text.Length == 0)
        {
            if (IsBlockedFinishReason(finishReason))
            {
                throw new AiProviderException(
                    AiFailureKind.Blocked,
                    "El proveedor bloqueó la respuesta.");
            }

            throw new AiProviderException(
                AiFailureKind.InvalidResponse,
                "El proveedor devolvió una respuesta sin texto.");
        }

        var promptTokens = 0;
        var candidateTokens = 0;
        var totalTokens = 0;

        if (root.TryGetProperty("usageMetadata", out var usage) &&
            usage.ValueKind == JsonValueKind.Object)
        {
            promptTokens = ReadInt(usage, "promptTokenCount");
            candidateTokens = ReadInt(usage, "candidatesTokenCount");
            totalTokens = ReadInt(usage, "totalTokenCount");
        }

        return new AiGenerationResult(
            text,
            finishReason,
            promptTokens,
            candidateTokens,
            totalTokens);
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        return 0;
    }

    private static bool IsBlockedFinishReason(string finishReason)
        => finishReason.Equals("SAFETY", StringComparison.OrdinalIgnoreCase) ||
           finishReason.Equals("RECITATION", StringComparison.OrdinalIgnoreCase) ||
           finishReason.Equals("BLOCKLIST", StringComparison.OrdinalIgnoreCase) ||
           finishReason.Equals("PROHIBITED_CONTENT", StringComparison.OrdinalIgnoreCase) ||
           finishReason.Equals("SPII", StringComparison.OrdinalIgnoreCase);

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
            (parsed.Scheme != Uri.UriSchemeHttps &&
             parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        baseUri = parsed;
        return true;
    }

    private static string NormalizeModelName(string model)
        => model.Trim().StartsWith(
            "models/",
            StringComparison.OrdinalIgnoreCase)
            ? model.Trim()["models/".Length..]
            : model.Trim();

    private static async Task<List<string>> ReadModelNamesAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var names = new List<string>();

        if (!document.RootElement.TryGetProperty("models", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var model in models.EnumerateArray())
        {
            if (model.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String)
            {
                var value = name.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    names.Add(NormalizeModelName(value));
                }
            }
        }

        return names;
    }

    private static async Task<string> ReadGeminiErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return Truncate(message.GetString(), 300);
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

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
    }
}
