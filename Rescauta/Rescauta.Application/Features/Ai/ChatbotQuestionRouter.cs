using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Rescauta.Application.Features.Ai;

public enum ChatbotPublicQueryKind
{
    CountComedores,
    ListComedores,
    DistrictsWithComedores
}

/// <summary>
/// Reconoce solo formulaciones concretas sobre cantidad, directorio y distritos.
/// Las demás preguntas continúan por el flujo documental habitual.
/// </summary>
public sealed class ChatbotQuestionRouter
{
    private static readonly Regex CountPattern = new(
        @"^(?:(?:dime|indica|muestra|por favor) )?(?:cuantos comedores (?:hay|existen|estan registrados|estan disponibles|hay registrados)|numero de comedores|(?:el )?numero de comedores|total de comedores|cuantos comedores registrados hay)(?: en rescauta)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ListPattern = new(
        @"^(?:(?:dime|indica|muestra|lista|por favor) )?(?:que comedores (?:hay|estan registrados|estan disponibles|existen)|cuales (?:son los )?comedores(?: registrados| disponibles| estan registrados| hay)?|lista de comedores|comedores registrados|comedores disponibles)(?: en rescauta)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DistrictPattern = new(
        @"^(?:(?:dime|indica|muestra|por favor) )?(?:en que distritos (?:hay|existen|estan|se encuentran) comedores|en que distritos se encuentran los comedores|en que distritos hay comedores registrados|que distritos tienen comedores|distritos con comedores)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RestrictedTopicPattern = new(
        @"\b(?:donar|donacion(?:es)?|donante|stock|inventario|kardex|insumo(?:s)?|alimento(?:s)?|telefono|direccion|contacto|operador(?:es)?|beneficiario(?:s)?|responsable|racion(?:es)?|abastecimiento|existencias?)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A hybrid route requires two independent, concrete signals: an institutional
    // how-to question and a public directory query. Merely mentioning comedores
    // never activates it.
    private static readonly Regex HybridDocumentIntentPattern = new(
        @"\b(?:como (?:puedo |se puede |se debe )?(?:donar|realizar una donacion)|como funciona rescauta|como funciona (?:la )?red|(?:que )?requisitos? (?:existen )?(?:hay )?(?:para |de )?(?:donar|donacion)|como (?:se )?hace una donacion)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HybridPublicIntentPattern = new(
        @"\b(?:cuantos comedores (?:hay|existen|estan registrados|estan disponibles)|numero de comedores|total de comedores|que comedores (?:hay|estan registrados|estan disponibles|existen)|cuales (?:son los )?comedores(?: registrados| disponibles| estan registrados| hay)?|en que distritos (?:hay|existen|estan|se encuentran) comedores|en que distritos se encuentran los comedores|que distritos tienen comedores|distritos con comedores)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HybridPrivateDataPattern = new(
        @"\b(?:stock|inventario|kardex|insumos?|telefonos?|direcciones?|coordenadas?|operadores?|responsables?|beneficiarios?|usuarios?|donantes?|datos personales|datos privados|contactos? personales|mis donaciones|mi donacion|donaciones? (?:individuales?|recibidas|de terceros|de (?:mi|un|una) (?:vecino|usuario|persona|donante))|registro de donaciones|estado de (?:mi |la )?donacion|seguimiento (?:de|a) (?:mi |la )?donacion|alimentos? (?:disponibles|en existencia|en stock)|necesidades? de (?:los )?comedores)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Identifica solo preguntas con una intención documental conocida y una
    /// consulta concreta del directorio público. Cualquier indicio de datos
    /// privados conserva el flujo documental y no habilita la consulta dinámica.
    /// </summary>
    public bool IsHybridQuestion(string? question)
    {
        var normalized = Normalize(question);

        return normalized.Length > 0 &&
               !HybridPrivateDataPattern.IsMatch(normalized) &&
               HybridDocumentIntentPattern.IsMatch(normalized) &&
               HybridPublicIntentPattern.IsMatch(normalized);
    }

    public ChatbotPublicQueryKind? Classify(string? question)
    {
        var normalized = Normalize(question);

        if (normalized.Length == 0 || RestrictedTopicPattern.IsMatch(normalized))
        {
            return null;
        }

        if (CountPattern.IsMatch(normalized))
        {
            return ChatbotPublicQueryKind.CountComedores;
        }

        if (ListPattern.IsMatch(normalized))
        {
            return ChatbotPublicQueryKind.ListComedores;
        }

        if (DistrictPattern.IsMatch(normalized))
        {
            return ChatbotPublicQueryKind.DistrictsWithComedores;
        }

        return null;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = true;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
