using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Construye un contexto acotado y determinista a partir de los fragmentos recuperados.
///
/// Reglas:
///   * deduplica por Id, por contentHash y por similitud de trigramas configurable,
///   * conserva fragmentos complementarios (el umbral es alto a propósito),
///   * limita fragmentos por fuente y tamaño total,
///   * asigna citas S1..Sn y escapa &amp;, &lt; y &gt; antes de insertar contenido.
/// </summary>
public sealed class RagContextBuilder(IOptions<AiRagOptions> options) : IRagContextBuilder
{
    public RagContext Empty => new();

    public RagContext Build(IReadOnlyList<DocumentSearchResult> fragments)
    {
        if (fragments is null || fragments.Count == 0)
        {
            return Empty;
        }

        var settings = options.Value;
        var maxFragments = Math.Max(1, settings.MaxFragments);
        var maxPerSource = Math.Max(1, settings.MaxFragmentsPerSource);
        var maxFragmentCharacters = Math.Max(100, settings.MaxFragmentCharacters);
        var maxContextCharacters = Math.Max(100, settings.MaxContextCharacters);
        var nearDuplicateThreshold = Math.Clamp(settings.NearDuplicateThreshold, 0.0, 1.0);

        var ordered = fragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Content))
            .OrderByDescending(fragment => fragment.Score)
            .ThenBy(fragment => fragment.Source, StringComparer.Ordinal)
            .ThenBy(fragment => fragment.Id)
            .ToArray();

        var selected = new List<(RagContextFragment Fragment, HashSet<string> Trigrams)>();
        var seenIds = new HashSet<Guid>();
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        var perSource = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var characterCount = 0;

        foreach (var candidate in ordered)
        {
            if (selected.Count >= maxFragments)
            {
                break;
            }

            if (!seenIds.Add(candidate.Id))
            {
                continue;
            }

            var content = PromptTextEscaper.NormalizeWhitespace(candidate.Content);

            if (content.Length == 0)
            {
                continue;
            }

            var contentHash = GetContentHash(candidate);

            if (!string.IsNullOrWhiteSpace(contentHash) && !seenHashes.Add(contentHash))
            {
                continue;
            }

            var comparisonText = NormalizeForComparison(content);
            var trigrams = BuildTrigrams(comparisonText);

            if (selected.Any(item =>
                    IsNearDuplicate(trigrams, item.Trigrams, nearDuplicateThreshold)))
            {
                continue;
            }

            var source = string.IsNullOrWhiteSpace(candidate.Source)
                ? "documento"
                : candidate.Source.Trim();

            perSource.TryGetValue(source, out var sourceCount);

            if (sourceCount >= maxPerSource)
            {
                continue;
            }

            var available = maxContextCharacters - characterCount;

            if (available <= 0)
            {
                break;
            }

            var fragment = BuildFragment(
                candidate,
                content,
                maxFragmentCharacters,
                available,
                selected.Count + 1);

            if (fragment is null)
            {
                continue;
            }

            selected.Add((fragment, trigrams));
            perSource[source] = sourceCount + 1;
            characterCount += fragment.Content.Length;
        }

        if (selected.Count == 0)
        {
            return Empty;
        }

        var text = new StringBuilder();

        foreach (var (fragment, _) in selected)
        {
            text.Append("<documento id=\"")
                .Append(fragment.CitationId)
                .Append("\" titulo=\"")
                .Append(PromptTextEscaper.EscapeAttribute(fragment.Title))
                .Append("\" fuente=\"")
                .Append(PromptTextEscaper.EscapeAttribute(fragment.Source))
                .Append("\" categoria=\"")
                .Append(PromptTextEscaper.EscapeAttribute(fragment.Category))
                .AppendLine("\">")
                .AppendLine(PromptTextEscaper.EscapeText(fragment.Content))
                .AppendLine("</documento>");
        }

        return new RagContext
        {
            Text = text.ToString().TrimEnd(),
            Fragments = selected.Select(item => item.Fragment).ToArray(),
            CharacterCount = characterCount
        };
    }

    private static RagContextFragment? BuildFragment(
        DocumentSearchResult candidate,
        string content,
        int maxFragmentCharacters,
        int availableCharacters,
        int citationNumber)
    {
        var limit = Math.Min(maxFragmentCharacters, availableCharacters);
        var truncated = TruncateAtWordBoundary(content, limit);

        if (string.IsNullOrWhiteSpace(truncated))
        {
            return null;
        }

        return new RagContextFragment
        {
            CitationId = $"S{citationNumber}",
            ChunkId = candidate.Id,
            Title = candidate.Title.Trim(),
            Source = candidate.Source.Trim(),
            Category = candidate.Category.Trim(),
            Content = truncated,
            Score = candidate.Score
        };
    }

    private static string TruncateAtWordBoundary(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var cut = value[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');

        if (lastSpace > maxLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd();
    }

    private static string GetContentHash(DocumentSearchResult fragment)
    {
        foreach (var pair in fragment.Metadata)
        {
            if (string.Equals(pair.Key, "contentHash", StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return string.Empty;
    }

    private static bool IsNearDuplicate(
        HashSet<string> candidate,
        HashSet<string> existing,
        double threshold)
    {
        if (threshold <= 0d)
        {
            return false;
        }

        if (candidate.Count == 0 || existing.Count == 0)
        {
            return false;
        }

        if (candidate.SetEquals(existing))
        {
            return true;
        }

        var intersection = candidate.Count(item => existing.Contains(item));

        if (intersection == 0)
        {
            return false;
        }

        var union = candidate.Count + existing.Count - intersection;
        var jaccard = union == 0 ? 0d : (double)intersection / union;

        return jaccard >= threshold;
    }

    private static string NormalizeForComparison(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static HashSet<string> BuildTrigrams(string value)
    {
        var trigrams = new HashSet<string>(StringComparer.Ordinal);

        if (value.Length == 0)
        {
            return trigrams;
        }

        if (value.Length < 3)
        {
            trigrams.Add(value);
            return trigrams;
        }

        for (var index = 0; index <= value.Length - 3; index++)
        {
            trigrams.Add(value.Substring(index, 3));
        }

        return trigrams;
    }
}
