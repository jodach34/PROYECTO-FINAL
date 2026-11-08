using System.Net;
using System.Text;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Escape centralizado para todo texto que entra al prompt.
/// El ampersand se reemplaza primero para evitar doble interpretación
/// (&lt; no debe convertirse en &amp;lt;).
/// </summary>
public static class PromptTextEscaper
{
    public static string EscapeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 16);

        foreach (var character in value)
        {
            switch (character)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    public static string EscapeAttribute(string? value)
        => EscapeText(value).Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>
    /// Normaliza caracteres de control y espacios sin alterar el significado del texto.
    /// No se usa para escapar: el escape se aplica después.
    /// </summary>
    public static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var builder = new StringBuilder(decoded.Length);
        var previousWasNewline = false;

        foreach (var character in decoded)
        {
            if (character == '\n')
            {
                if (!previousWasNewline)
                {
                    builder.Append('\n');
                }

                previousWasNewline = true;
                continue;
            }

            if (char.IsControl(character) && character != '\t')
            {
                continue;
            }

            builder.Append(character);
            previousWasNewline = false;
        }

        return builder.ToString().Trim();
    }
}
