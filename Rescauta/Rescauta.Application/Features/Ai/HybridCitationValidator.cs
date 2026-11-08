using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Features.Ai;

public enum HybridCitationRejectionReason
{
    None,
    NoCitations,
    UnknownIdentifier,
    FactualClaimWithoutCitation,
    UnsupportedFormat,
    InsufficientSource
}

public sealed record HybridCitationValidationResult(
    bool IsValid,
    IReadOnlySet<string> CitationIds,
    HybridCitationRejectionReason RejectionReason);

/// <summary>
/// Conservatively checks the shape, scope and coverage of citations in a hybrid answer.
/// It never logs or retains the answer text.
/// </summary>
public static class HybridCitationValidator
{
    private static readonly Regex BracketGroup = new(@"\[[^\[\]]*\]", RegexOptions.Compiled);
    private static readonly Regex CitationToken = new(
        @"(?<![\p{L}\d])([SD])\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ValidGroup = new(
        @"^(?:S\d+|D\d+)(?:\s*,\s*(?:S\d+|D\d+))*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DynamicClaim = new(
        @"\b(?:actual(?:mente)?|directorio|total(?: de)? comedores|cantidad de comedores|numero de comedores|cuantos? comedores|cuantas? comedores|comedores? (?:registrados?|disponibles?)|distritos? (?:con|donde|en)|en (?:los? )?distritos?)\b|\b(?:hay|existen|son|figuran)\s+(?:\d+|cero|un|una|uno|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|dieciseis|diecisiete|dieciocho|diecinueve|veinte)?\s*comedores?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex InstitutionalClaim = new(
        @"\b(?:donar|donacion(?:es)?|entregar|entrega|requisitos?|pasos?|proceso|guia|instrucciones?|registrar una donacion|como (?:puedo|se|realizar|hacer))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DirectoryCount = new(
        @"\b(?:(?:hay|existen|total(?: de)?|actualmente hay)\s+)?(?<count>\d+|cero|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|dieciseis|diecisiete|dieciocho|diecinueve|veinte)\s+comedores\b(?:\s+registrados?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownHeading = new(
        @"^\s{0,3}#{1,6}\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UnbracketedCitation = new(
        @"(?<![\p{L}\d])(?:S|D)\s*\d+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static HybridCitationValidationResult Validate(
        string text,
        RagContext context,
        ChatbotDynamicDataDto? publicData)
    {
        ArgumentNullException.ThrowIfNull(context);

        var validIds = context.Fragments
            .Select(fragment => fragment.CitationId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (publicData is not null)
        {
            validIds.Add("D1");
        }

        var citations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bracketSpans = new List<(int Start, int Length)>();

        foreach (Match group in BracketGroup.Matches(text))
        {
            bracketSpans.Add((group.Index, group.Length));
            var content = group.Value[1..^1];
            var tokens = CitationToken.Matches(content);
            if (tokens.Count == 0)
            {
                continue;
            }

            var normalized = Regex.Replace(content, @"\s+", string.Empty);
            if (!ValidGroup.IsMatch(normalized))
            {
                return Reject(HybridCitationRejectionReason.UnsupportedFormat, citations);
            }

            foreach (Match token in tokens)
            {
                var id = token.Groups[1].Value.ToUpperInvariant() + token.Groups[2].Value;
                if (!validIds.Contains(id))
                {
                    return Reject(HybridCitationRejectionReason.UnknownIdentifier, citations);
                }

                citations.Add(id);
            }
        }

        var withoutGroups = new StringBuilder(text);
        foreach (var span in bracketSpans.AsEnumerable().Reverse())
        {
            withoutGroups.Remove(span.Start, span.Length);
        }

        if (UnbracketedCitation.IsMatch(withoutGroups.ToString()))
        {
            return Reject(HybridCitationRejectionReason.UnsupportedFormat, citations);
        }

        if (citations.Count == 0)
        {
            return Reject(HybridCitationRejectionReason.NoCitations, citations);
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            foreach (var sentence in SplitSentences(line))
            {
                var claim = sentence.Trim();
                if (claim.Length == 0 || IsDecorativeHeading(claim))
                {
                    continue;
                }

                var sentenceCitations = GetSentenceCitations(claim);
                if (sentenceCitations.Count == 0)
                {
                    return Reject(HybridCitationRejectionReason.FactualClaimWithoutCitation, citations);
                }

                if (!CitationIsAtSentenceEnd(claim))
                {
                    return Reject(HybridCitationRejectionReason.UnsupportedFormat, citations);
                }

                var needsDocument = InstitutionalClaim.IsMatch(claim);
                var needsDirectory = IsDirectoryClaim(claim, publicData);
                var hasDocument = sentenceCitations.Any(id => id.StartsWith('S'));
                var hasDirectory = sentenceCitations.Contains("D1");

                if ((needsDocument && !hasDocument) || (needsDirectory && !hasDirectory) ||
                    (needsDirectory && !needsDocument && hasDocument) ||
                    (needsDocument && !needsDirectory && hasDirectory) ||
                    (needsDirectory && publicData is not null && !DirectoryCountMatchesData(claim, publicData)))
                {
                    return Reject(HybridCitationRejectionReason.InsufficientSource, citations);
                }
            }
        }

        return new HybridCitationValidationResult(true, citations, HybridCitationRejectionReason.None);
    }

    private static IReadOnlySet<string> GetSentenceCitations(string sentence)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match group in BracketGroup.Matches(sentence))
        {
            var content = Regex.Replace(group.Value[1..^1], @"\s+", string.Empty);
            if (!ValidGroup.IsMatch(content))
            {
                continue;
            }

            foreach (var id in content.Split(','))
            {
                ids.Add(id.ToUpperInvariant());
            }
        }

        return ids;
    }

    private static bool CitationIsAtSentenceEnd(string sentence)
    {
        var matches = BracketGroup.Matches(sentence)
            .Cast<Match>()
            .Where(match => CitationToken.IsMatch(match.Value))
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        var tail = sentence[(matches[^1].Index + matches[^1].Length)..];
        return tail.All(character => char.IsWhiteSpace(character) ||
            char.IsPunctuation(character) || character is '”' or '’' or '»');
    }

    private static bool IsDirectoryClaim(string sentence, ChatbotDynamicDataDto? data)
    {
        if (DynamicClaim.IsMatch(Normalize(sentence)))
        {
            return true;
        }

        if (data is null)
        {
            return false;
        }

        var normalized = Normalize(sentence);
        return (data.Comedores ?? [])
                   .Any(comedor => ContainsNormalized(normalized, comedor.Nombre)) ||
               (data.Distritos ?? [])
                   .Any(district => ContainsNormalized(normalized, district));
    }

    private static bool DirectoryCountMatchesData(string sentence, ChatbotDynamicDataDto data)
    {
        var match = DirectoryCount.Match(Normalize(sentence));
        if (!match.Success || !int.TryParse(match.Groups["count"].Value, out var count))
        {
            if (!match.Success)
            {
                return true;
            }

            count = match.Groups["count"].Value switch
            {
                "cero" => 0,
                "un" or "una" or "uno" => 1,
                "dos" => 2,
                "tres" => 3,
                "cuatro" => 4,
                "cinco" => 5,
                "seis" => 6,
                "siete" => 7,
                "ocho" => 8,
                "nueve" => 9,
                "diez" => 10,
                "once" => 11,
                "doce" => 12,
                "trece" => 13,
                "catorce" => 14,
                "quince" => 15,
                "dieciseis" => 16,
                "diecisiete" => 17,
                "dieciocho" => 18,
                "diecinueve" => 19,
                "veinte" => 20,
                _ => -1
            };
        }

        return count == Math.Max(0, data.TotalComedores);
    }

    private static bool ContainsNormalized(string normalizedText, string value)
    {
        var normalizedValue = Normalize(value);
        return normalizedValue.Length > 0 && normalizedText.Contains(normalizedValue, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
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
                builder.Append(char.ToLowerInvariant(character));
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

    private static IEnumerable<string> SplitSentences(string line)
    {
        var protectedLine = Regex.Replace(
            line,
            @"\bp\.\s*ej\.(?=\s+\p{Ll})",
            match => match.Value.Replace('.', '\uE000'),
            RegexOptions.CultureInvariant);
        protectedLine = Regex.Replace(
            protectedLine,
            @"\b(?:Sr|Sra|Srta|Dr|Dra|Prof|Av|Aprox|Num|Nro|Dpto|P)\.",
            match => match.Value.Replace('.', '\uE000'),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        protectedLine = Regex.Replace(protectedLine, @"(?<=\d)\.(?=\d)", "\uE001");

        return Regex.Split(protectedLine, @"(?<=[.!?])\s+")
            .Select(value => value.Replace('\uE000', '.').Replace('\uE001', '.'));
    }

    private static bool IsDecorativeHeading(string sentence)
    {
        if (MarkdownHeading.IsMatch(sentence) && !Regex.IsMatch(sentence, @"\d"))
        {
            return true;
        }

        return sentence.EndsWith(':') && sentence.Length <= 100 &&
               !Regex.IsMatch(sentence, @"\d") &&
               !DynamicClaim.IsMatch(Normalize(sentence)) &&
               !InstitutionalClaim.IsMatch(Normalize(sentence));
    }

    private static HybridCitationValidationResult Reject(
        HybridCitationRejectionReason reason,
        IReadOnlySet<string> citations)
        => new(false, citations, reason);
}
