using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Tests;

/// <summary>
/// Utilidades compartidas por las pruebas. No contienen claves reales ni
/// credenciales: los valores usados aquí son de laboratorio.
/// </summary>
internal static class TestSupport
{
    public static IOptions<T> Options<T>(T value) where T : class
        => Microsoft.Extensions.Options.Options.Create(value);

    public static DocumentSearchResult Fragment(
        Guid? id = null,
        string title = "Documento de prueba",
        string content = "Contenido de prueba suficientemente largo para el contexto.",
        string source = "documento-prueba",
        string category = "general",
        double score = 0.9,
        string? contentHash = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(contentHash))
        {
            metadata["contentHash"] = contentHash;
        }

        return new DocumentSearchResult
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Content = content,
            Source = source,
            Category = category,
            Score = score,
            Metadata = metadata
        };
    }

    public static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}

/// <summary>
/// Logger que conserva los mensajes formateados para verificar que no se
/// registran preguntas, respuestas completas, prompts ni claves.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));

        if (exception is not null)
        {
            Messages.Add(exception.Message);
        }
    }
}

/// <summary>
/// Handler HTTP falso: permite simular respuestas, errores y timeouts sin red.
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return await handler(request, cancellationToken);
    }
}
