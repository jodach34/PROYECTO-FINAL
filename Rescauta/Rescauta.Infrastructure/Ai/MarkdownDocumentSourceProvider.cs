using System.Reflection;
using System.Text;
using System.Text.Json;
using Rescauta.Application.Features.Ai.Models;
using Rescauta.Application.Interfaces.Ai;

namespace Rescauta.Infrastructure.Ai;

/// <summary>
/// Proveedor de documentos institucionales curados.
///
/// Los Markdown y el manifiesto index.json se compilan como recursos embebidos en este
/// ensamblado. Así viajan dentro de la imagen Docker de la API y no dependen de rutas
/// del sistema de archivos ni del proyecto MVC.
///
/// Solo entrega documentación pública de Rescauta: no lee bases de datos, usuarios,
/// donaciones reales ni secretos.
/// </summary>
public sealed class MarkdownDocumentSourceProvider : IDocumentSourceProvider
{
    private const string ResourcePrefix = "Rescauta.Infrastructure.Ai.Documents.";
    private const string ManifestResourceName = ResourcePrefix + "index.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Assembly ResourceAssembly =
        typeof(MarkdownDocumentSourceProvider).Assembly;

    public IReadOnlyList<DocumentSource> GetDocuments()
    {
        var manifest = ReadResource(ManifestResourceName);
        var entries = JsonSerializer.Deserialize<List<DocumentManifestEntry>>(
            manifest,
            JsonOptions);

        if (entries is null || entries.Count == 0)
        {
            throw new InvalidOperationException(
                "El manifiesto de documentos institucionales está vacío.");
        }

        var documents = new List<DocumentSource>(entries.Count);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Source) ||
                string.IsNullOrWhiteSpace(entry.File) ||
                string.IsNullOrWhiteSpace(entry.Title))
            {
                throw new InvalidOperationException(
                    "El manifiesto contiene una entrada sin source, file o title.");
            }

            if (!sources.Add(entry.Source.Trim()))
            {
                throw new InvalidOperationException(
                    $"El manifiesto repite la fuente '{entry.Source}'.");
            }

            var resourceName = ResourcePrefix + entry.File.Trim();
            var content = ReadResource(resourceName);

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException(
                    $"El documento embebido '{entry.File}' está vacío.");
            }

            documents.Add(new DocumentSource
            {
                Source = entry.Source.Trim(),
                Title = entry.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(entry.Category)
                    ? "institucional"
                    : entry.Category.Trim(),
                Content = content,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["audience"] = "public",
                    ["language"] = "es",
                    ["contentType"] = "institucional"
                }
            });
        }

        return documents;
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"No se encontró el recurso embebido '{resourceName}'. " +
                "Revisa Rescauta.Infrastructure.csproj y el manifiesto index.json.");
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private sealed record DocumentManifestEntry(
        string Source,
        string File,
        string Title,
        string Category);
}
