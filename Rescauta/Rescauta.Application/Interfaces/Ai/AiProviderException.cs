namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Clasificación segura de un fallo del proveedor de IA.
/// Permite que la capa de aplicación traduzca el error a HTTP sin inspeccionar
/// mensajes del proveedor ni exponer cuerpos de respuesta.
/// </summary>
public enum AiFailureKind
{
    NotConfigured,
    Timeout,
    Network,
    InvalidResponse,
    Blocked,
    QuotaExceeded,
    UpstreamError
}

/// <summary>
/// Error controlado de Gemini. El mensaje es siempre genérico y apto para logs;
/// nunca contiene la ApiKey, la URL interna ni el cuerpo completo del proveedor.
/// </summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(
        AiFailureKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public AiFailureKind Kind { get; }
}
