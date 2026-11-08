using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Fragmenta Markdown institucional por secciones y párrafos.
///
/// Decisiones:
///   * Cada fragmento incluye el título del documento y el título de su sección para que
///     el embedding tenga contexto aunque el fragmento se recupere aislado.
///   * El Id se deriva del hash del contenido completo y del índice del fragmento, así
///     que reindexar el mismo texto produce exactamente los mismos puntos.
///   * El solapamiento se toma del final del fragmento anterior, cortado en un espacio,
///     para no repetir texto a medias.
/// </summary>
public sealed class DocumentChunker(IOptions<AiIndexingOptions> options) : IDocumentChunker
{
    private const int AbsoluteMinimumBodyLength = 20;

    private static readonly Regex HeadingRegex = new(
        @"^(#{1,6})\s+(.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public IReadOnlyList<DocumentChunk> Chunk(DocumentSource document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.Source))
        {
            throw new ArgumentException(
                "El documento debe tener un identificador de fuente.",
                nameof(document));
        }

        if (string.IsNullOrWhiteSpace(document.Content))
        {
            return Array.Empty<DocumentChunk>();
        }

        var settings = options.Value;
        var maxCharacters = Math.Max(200, settings.ChunkMaxCharacters);
        var overlap = Math.Clamp(settings.ChunkOverlapCharacters, 0, maxCharacters / 2);
        var minCharacters = Math.Clamp(
            settings.ChunkMinCharacters,
            AbsoluteMinimumBodyLength,
            maxCharacters);

        var normalized = Normalize(document.Content);
        var contentHash = ComputeContentHash(
            document.Source,
            document.Title,
            document.Category,
            normalized);

        var sections = ParseSections(normalized);
        var prepared = new List<(string Section, string Body)>(sections.Count);

        foreach (var section in sections)
        {
            var prefix = BuildPrefix(document.Title, section.Title);
            var bodyBudget = Math.Max(80, maxCharacters - prefix.Length);

            foreach (var body in SplitSection(section.Body, bodyBudget, overlap))
            {
                var trimmed = body.Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                prepared.Add((section.Title, trimmed));
            }
        }

        // Se descartan fragmentos demasiado cortos, salvo que fueran el único contenido
        // útil del documento. Así no se pierde una respuesta breve pero válida.
        var kept = prepared
            .Where(item => item.Body.Length >= minCharacters)
            .ToList();

        if (kept.Count == 0 && prepared.Count > 0)
        {
            kept.Add(prepared.OrderByDescending(item => item.Body.Length).First());
        }

        var chunks = new List<DocumentChunk>(kept.Count);

        for (var index = 0; index < kept.Count; index++)
        {
            var (sectionTitle, body) = kept[index];
            var content = (BuildPrefix(document.Title, sectionTitle) + body).Trim();

            if (content.Length > maxCharacters)
            {
                content = content[..maxCharacters].TrimEnd();
            }

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["section"] = sectionTitle,
                ["chunkIndex"] = index.ToString(CultureInfo.InvariantCulture),
                ["chunkCount"] = kept.Count.ToString(CultureInfo.InvariantCulture),
                ["contentHash"] = contentHash,
                ["documentSource"] = document.Source
            };

            if (document.Metadata is not null)
            {
                foreach (var pair in document.Metadata)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
                    {
                        metadata[pair.Key] = pair.Value;
                    }
                }
            }

            chunks.Add(new DocumentChunk
            {
                Id = CreateChunkId(document.Source, contentHash, index),
                Title = document.Title,
                Content = content,
                Source = document.Source,
                Category = document.Category,
                Metadata = metadata
            });
        }

        return chunks;
    }

    private static List<MarkdownSection> ParseSections(string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sections = new List<MarkdownSection>();
        var title = string.Empty;
        var body = new StringBuilder();

        void Flush()
        {
            var text = body.ToString().Trim();

            if (text.Length > 0)
            {
                sections.Add(new MarkdownSection(title, text));
            }

            body.Clear();
        }

        foreach (var line in lines)
        {
            var match = HeadingRegex.Match(line);

            if (match.Success)
            {
                Flush();
                title = match.Groups[2].Value.Trim();
                continue;
            }

            body.AppendLine(line);
        }

        Flush();

        if (sections.Count == 0)
        {
            sections.Add(new MarkdownSection(string.Empty, content.Trim()));
        }

        return sections;
    }

    private static IReadOnlyList<string> SplitSection(string body, int maxCharacters, int overlap)
    {
        var normalized = body.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        if (normalized.Length <= maxCharacters)
        {
            return new[] { normalized };
        }

        var segments = new List<string>();

        foreach (var paragraph in SplitParagraphs(normalized))
        {
            if (paragraph.Length <= maxCharacters)
            {
                segments.Add(paragraph);
                continue;
            }

            segments.AddRange(HardSplit(paragraph, maxCharacters));
        }

        var chunks = new List<string>();
        var current = new StringBuilder();

        void FlushCurrent()
        {
            var text = current.ToString().Trim();
            current.Clear();

            if (text.Length > 0)
            {
                chunks.Add(text);
            }
        }

        foreach (var segment in segments)
        {
            if (current.Length == 0)
            {
                current.Append(segment);
                continue;
            }

            if (current.Length + 2 + segment.Length <= maxCharacters)
            {
                current.Append("\n\n").Append(segment);
                continue;
            }

            FlushCurrent();

            var tail = ExtractOverlap(chunks[^1], overlap);

            if (tail.Length > 0 && tail.Length + 2 + segment.Length <= maxCharacters)
            {
                current.Append(tail).Append("\n\n").Append(segment);
            }
            else
            {
                current.Append(segment);
            }
        }

        FlushCurrent();

        return chunks;
    }

    private static IReadOnlyList<string> SplitParagraphs(string text)
    {
        var paragraphs = new List<string>();
        var current = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                var paragraph = current.ToString().Trim();

                if (paragraph.Length > 0)
                {
                    paragraphs.Add(paragraph);
                }

                current.Clear();
                continue;
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }

            current.Append(line.TrimEnd());
        }

        var last = current.ToString().Trim();

        if (last.Length > 0)
        {
            paragraphs.Add(last);
        }

        return paragraphs;
    }

    private static IReadOnlyList<string> HardSplit(string text, int maxCharacters)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > maxCharacters)
            {
                var rest = word;

                while (rest.Length > maxCharacters)
                {
                    if (current.Length > 0)
                    {
                        pieces.Add(current.ToString().Trim());
                        current.Clear();
                    }

                    pieces.Add(rest[..maxCharacters]);
                    rest = rest[maxCharacters..];
                }

                if (rest.Length > 0)
                {
                    current.Append(rest);
                }

                continue;
            }

            if (current.Length > 0 && current.Length + 1 + word.Length > maxCharacters)
            {
                pieces.Add(current.ToString().Trim());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        var last = current.ToString().Trim();

        if (last.Length > 0)
        {
            pieces.Add(last);
        }

        return pieces;
    }

    private static string ExtractOverlap(string text, int overlap)
    {
        if (overlap <= 0 || text.Length == 0)
        {
            return string.Empty;
        }

        var start = Math.Max(0, text.Length - overlap);
        var slice = text[start..];

        // No se empieza en medio de una palabra.
        var firstSpace = slice.IndexOf(' ');

        if (start > 0 && firstSpace >= 0 && firstSpace + 1 < slice.Length)
        {
            slice = slice[(firstSpace + 1)..];
        }

        slice = slice.Trim();

        return slice.Length >= AbsoluteMinimumBodyLength ? slice : string.Empty;
    }

    private static string BuildPrefix(string documentTitle, string sectionTitle)
    {
        var title = NormalizeInline(documentTitle);
        var section = NormalizeInline(sectionTitle);

        if (string.IsNullOrWhiteSpace(title))
        {
            return string.IsNullOrWhiteSpace(section)
                ? string.Empty
                : $"## {section}\n\n";
        }

        if (string.IsNullOrWhiteSpace(section) ||
            string.Equals(section, title, StringComparison.OrdinalIgnoreCase))
        {
            return $"# {title}\n\n";
        }

        return $"# {title}\n\n## {section}\n\n";
    }

    private static string Normalize(string value)
        => value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static string NormalizeInline(string value)
        => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static string ComputeContentHash(
        string source,
        string title,
        string category,
        string content)
    {
        var material = string.Join(
            '\n',
            NormalizeInline(source),
            NormalizeInline(title),
            NormalizeInline(category),
            content);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Guid CreateChunkId(string source, string contentHash, int chunkIndex)
    {
        var material = $"{source}|{contentHash}|{chunkIndex.ToString(CultureInfo.InvariantCulture)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return new Guid(hash.AsSpan(0, 16));
    }

    private readonly record struct MarkdownSection(string Title, string Body);
}
