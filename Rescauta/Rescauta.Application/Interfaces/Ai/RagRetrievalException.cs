namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Clasificación de fallos durante la recuperación documental.
/// La recuperación es de solo lectura: estos errores nunca implican escrituras en Qdrant.
/// </summary>
public enum RagRetrievalFailure
{
    AiNotConfigured,
    AiTimeout,
    AiUnavailable,
    AiInvalidResponse,
    QdrantUnavailable,
    QdrantTimeout
}

/// <summary>
/// Error controlado de la recuperación. El mensaje es genérico y no incluye
/// credenciales, URLs internas ni cuerpos completos de Gemini/Qdrant.
/// </summary>
public sealed class RagRetrievalException : Exception
{
    public RagRetrievalException(
        RagRetrievalFailure failure,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    public RagRetrievalFailure Failure { get; }
}
