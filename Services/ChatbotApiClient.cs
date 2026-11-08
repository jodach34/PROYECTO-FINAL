using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PROYECTO_FINAL.Models.Api;

namespace PROYECTO_FINAL.Services;

public enum ChatbotApiCallStatus
{
    Success,
    Canceled,
    ClientTimeout,
    ConnectionError,
    ApiError,
    ApiTimeout
}

public sealed record ChatbotApiResult(
    ChatbotApiCallStatus Status,
    ChatbotProxyResponse? Response = null,
    HttpStatusCode? ApiStatusCode = null,
    string? ApiErrorCode = null)
{
    public static ChatbotApiResult Succeeded(ChatbotProxyResponse response) =>
        new(ChatbotApiCallStatus.Success, response);

    public static ChatbotApiResult Canceled() =>
        new(ChatbotApiCallStatus.Canceled);

    public static ChatbotApiResult ClientTimedOut() =>
        new(ChatbotApiCallStatus.ClientTimeout);

    public static ChatbotApiResult ConnectionFailed() =>
        new(ChatbotApiCallStatus.ConnectionError);

    public static ChatbotApiResult ApiFailed(HttpStatusCode statusCode, string? errorCode) =>
        new(
            statusCode == HttpStatusCode.GatewayTimeout
                ? ChatbotApiCallStatus.ApiTimeout
                : ChatbotApiCallStatus.ApiError,
            ApiStatusCode: statusCode,
            ApiErrorCode: errorCode);
}

/// <summary>
/// Cliente HTTP exclusivo del chatbot. No comparte timeout ni manejo de errores con
/// RescautaApiClient, porque el RAG necesita más tiempo que las operaciones normales.
/// </summary>
public sealed class ChatbotApiClient(
    HttpClient httpClient,
    ILogger<ChatbotApiClient> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ChatbotApiResult> PreguntarAsync(
        ChatbotProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/api/v1/chatbot/preguntar",
                request,
                Json,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<ChatbotProxyResponse>(
                    Json,
                    cancellationToken);

                return payload is null
                    ? ChatbotApiResult.ApiFailed(HttpStatusCode.BadGateway, "respuesta_vacia")
                    : ChatbotApiResult.Succeeded(payload);
            }

            var error = await LeerErrorAsync(response, cancellationToken);
            return ChatbotApiResult.ApiFailed(response.StatusCode, error?.Codigo);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("La consulta del chatbot fue cancelada por el cliente.");
            return ChatbotApiResult.Canceled();
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("El cliente HTTP del chatbot excedio su timeout de 60 segundos.");
            return ChatbotApiResult.ClientTimedOut();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "No se pudo conectar con la API del chatbot.");
            return ChatbotApiResult.ConnectionFailed();
        }
    }

    private static async Task<ChatbotProxyError?> LeerErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ChatbotProxyError>(
                Json,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
