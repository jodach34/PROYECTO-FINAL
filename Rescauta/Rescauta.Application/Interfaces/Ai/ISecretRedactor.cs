namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Comprueba y redacta secretos configurados (Gemini/Qdrant) en textos generados.
/// La implementación conoce las claves; Application solo ve esta abstracción.
/// </summary>
public interface ISecretRedactor
{
    bool ContainsSecret(string text);

    string Redact(string text);
}
