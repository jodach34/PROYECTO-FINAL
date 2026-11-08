using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Rescauta.Application.Interfaces.Ai;
using Rescauta.Infrastructure.Options;

namespace Rescauta.Infrastructure.Ai;

/// <summary>
/// Comprueba si un texto generado contiene las claves configuradas de Gemini/Qdrant
/// o un patrón reconocible de clave de Google. La comparación es en memoria y el
/// valor del secreto nunca se registra.
/// </summary>
public sealed class ConfiguredSecretRedactor : ISecretRedactor
{
    private static readonly Regex GoogleApiKeyPattern = new(
        @"AIza[0-9A-Za-z_\-]{30,}",
        RegexOptions.Compiled);

    private readonly string[] _secrets;

    public ConfiguredSecretRedactor(
        IOptions<GeminiOptions> geminiOptions,
        IOptions<QdrantOptions> qdrantOptions)
    {
        _secrets = new[]
            {
                geminiOptions.Value.ApiKey,
                qdrantOptions.Value.ApiKey
            }
            .Where(secret => !string.IsNullOrWhiteSpace(secret) && secret.Length >= 8)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(secret => secret.Length)
            .ToArray();
    }

    public bool ContainsSecret(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (GoogleApiKeyPattern.IsMatch(text))
        {
            return true;
        }

        return _secrets.Any(secret =>
            text.Contains(secret, StringComparison.Ordinal));
    }

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var redacted = GoogleApiKeyPattern.Replace(text, "[REDACTADO]");

        foreach (var secret in _secrets)
        {
            redacted = redacted.Replace(
                secret,
                "[REDACTADO]",
                StringComparison.Ordinal);
        }

        return redacted;
    }
}
