namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Resultado sanitizado de una comprobacion de conectividad contra un proveedor de IA.
///
/// Regla de seguridad: NUNCA se incluye la ApiKey, el token ni la URL con credenciales.
/// Solo viaja lo necesario para diagnosticar: si la configuracion esta completa, si el
/// servicio respondio y si la respuesta es la esperada.
/// </summary>
public sealed record AiConnectivityResult(
    string Service,
    bool IsConfigured,
    bool IsReachable,
    bool IsHealthy,
    string Message,
    IReadOnlyList<string> Details);

/// <summary>
/// Resumen de las dos dependencias del chatbot. Se expone en el endpoint de diagnostico
/// para comprobar Gemini y Qdrant de una sola llamada.
/// </summary>
public sealed record AiConnectivityReport(
    bool AllHealthy,
    AiConnectivityResult Gemini,
    AiConnectivityResult Qdrant);
