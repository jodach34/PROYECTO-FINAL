using Rescauta.Application.Features.Ai;
using Rescauta.Application.Features.Ai.Models;
using Xunit;

namespace Rescauta.Tests;

public sealed class RagContextBuilderTests
{
    private static RagContextBuilder CreateBuilder(AiRagOptions? options = null)
        => new(TestSupport.Options(options ?? new AiRagOptions()));

    [Fact]
    public void Build_Vacio_DevuelveContextoVacio()
    {
        var builder = CreateBuilder();

        var context = builder.Build(Array.Empty<DocumentSearchResult>());

        Assert.Empty(context.Fragments);
        Assert.Equal(string.Empty, context.Text);
        Assert.Equal(0, context.CharacterCount);
    }

    [Fact]
    public void Build_EscapaAmpersandMenorYMayorSinPermitirCerrarEtiquetas()
    {
        var builder = CreateBuilder();
        var fragment = TestSupport.Fragment(
            content: "Texto & <script>alert(1)</script> </documento> &lt;b&gt;");

        var context = builder.Build([fragment]);

        Assert.Contains("&amp;", context.Text);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", context.Text);
        Assert.Contains("&lt;/documento&gt;", context.Text);
        Assert.DoesNotContain("<script>", context.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountOccurrences(context.Text, "</documento>"));
        Assert.Equal(1, CountOccurrences(context.Text, "<documento id="));
    }

    [Fact]
    public void Build_DeduplicaPorId()
    {
        var builder = CreateBuilder();
        var id = Guid.NewGuid();

        var context = builder.Build([
            TestSupport.Fragment(id: id, content: "Primer contenido distinto y suficientemente largo."),
            TestSupport.Fragment(id: id, content: "Segundo contenido distinto y suficientemente largo.")
        ]);

        Assert.Single(context.Fragments);
    }

    [Fact]
    public void Build_DeduplicaPorContentHash()
    {
        var builder = CreateBuilder();

        var context = builder.Build([
            TestSupport.Fragment(content: "Contenido A completamente distinto del contenido B.", contentHash: "hash-1"),
            TestSupport.Fragment(content: "Texto B sin relación textual con el anterior.", contentHash: "hash-1")
        ]);

        Assert.Single(context.Fragments);
    }

    [Fact]
    public void Build_DeduplicaCasiDuplicadosPorTrigramas()
    {
        var builder = CreateBuilder(new AiRagOptions { NearDuplicateThreshold = 0.90 });
        const string content =
            "Las donaciones de alimentos se registran en el banco de alimentos y luego " +
            "se distribuyen entre los comedores comunitarios registrados.";

        var context = builder.Build([
            TestSupport.Fragment(content: content, score: 0.95),
            TestSupport.Fragment(content: content, score: 0.94),
            TestSupport.Fragment(content: content + " ", score: 0.93)
        ]);

        Assert.Single(context.Fragments);
    }

    [Fact]
    public void Build_ConservaFragmentosComplementarios()
    {
        var builder = CreateBuilder(new AiRagOptions { NearDuplicateThreshold = 0.95 });

        const string donaciones =
            "Para donar alimentos debes registrar la donación con el tipo de insumo, " +
            "la cantidad y la fecha de entrega en el formulario del banco de alimentos. " +
            "El sistema genera un código de seguimiento único para cada donación recibida.";

        const string comedores =
            "Los comedores comunitarios deben actualizar su capacidad diaria y el número " +
            "de beneficiarios atendidos. El coordinador revisa esa información antes de " +
            "aprobar una nueva entrega de insumos perecederos.";

        var context = builder.Build([
            TestSupport.Fragment(content: donaciones, source: "donaciones", score: 0.9),
            TestSupport.Fragment(content: comedores, source: "comedores", score: 0.8)
        ]);

        Assert.Equal(2, context.Fragments.Count);
        Assert.Contains(context.Fragments, fragment => fragment.Source == "donaciones");
        Assert.Contains(context.Fragments, fragment => fragment.Source == "comedores");
    }

    [Fact]
    public void Build_AsignaCitasSecuencialesYOrdenDeterminista()
    {
        var builder = CreateBuilder(new AiRagOptions { MaxFragments = 5 });

        var context = builder.Build([
            TestSupport.Fragment(
                id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
                source: "zeta",
                content: "La entrega de alimentos perecederos se coordina con el transporte refrigerado."),
            TestSupport.Fragment(
                id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                source: "alfa",
                content: "El registro de donaciones exige cantidad, insumo y fecha de recepción."),
            TestSupport.Fragment(
                id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
                source: "alfa",
                content: "Los comedores actualizan su capacidad y el número de beneficiarios atendidos.")
        ]);

        Assert.Equal(3, context.Fragments.Count);
        Assert.Equal("S1", context.Fragments[0].CitationId);
        Assert.Equal("alfa", context.Fragments[0].Source);
        Assert.Equal("S2", context.Fragments[1].CitationId);
        Assert.Equal("S3", context.Fragments[2].CitationId);
        Assert.Equal(
            context.Fragments.OrderBy(fragment => fragment.CitationId).Select(fragment => fragment.CitationId),
            context.Fragments.Select(fragment => fragment.CitationId));
    }

    [Fact]
    public void Build_RespetaMaximoDeFragmentos()
    {
        var builder = CreateBuilder(new AiRagOptions { MaxFragments = 2 });

        var context = builder.Build(EnumerarFragmentosDistintos(4));

        Assert.Equal(2, context.Fragments.Count);
    }

    [Fact]
    public void Build_RespetaMaximoPorFuente()
    {
        var builder = CreateBuilder(new AiRagOptions
        {
            MaxFragments = 5,
            MaxFragmentsPerSource = 2
        });

        var context = builder.Build(EnumerarFragmentosDistintos(4));

        Assert.Equal(2, context.Fragments.Count);
        Assert.All(context.Fragments, fragment => Assert.Equal("misma-fuente", fragment.Source));
    }

    [Fact]
    public void Build_TruncaFragmentosLargosSinCortarPalabrasRaras()
    {
        var builder = CreateBuilder(new AiRagOptions
        {
            MaxFragmentCharacters = 120,
            MaxContextCharacters = 6000
        });

        var content = string.Join(' ', Enumerable.Repeat("palabra", 200));

        var context = builder.Build([TestSupport.Fragment(content: content)]);

        var fragment = Assert.Single(context.Fragments);
        Assert.True(fragment.Content.Length <= 120);
        Assert.False(fragment.Content.EndsWith(' '));
        Assert.DoesNotContain("  ", fragment.Content);
    }

    [Fact]
    public void Build_IgnoraContenidoVacio()
    {
        var builder = CreateBuilder();

        var context = builder.Build([
            TestSupport.Fragment(content: "   ")
        ]);

        Assert.Empty(context.Fragments);
    }

    private static IReadOnlyList<DocumentSearchResult> EnumerarFragmentosDistintos(int count)
    {
        string[] contents =
        [
            "Las donaciones de alimentos no perecederos se reciben en el banco de alimentos central.",
            "Cada comedor comunitario debe informar cuántas personas atendió durante la jornada.",
            "El código de seguimiento permite consultar el estado logístico de una donación.",
            "La privacidad de los beneficiarios exige no publicar nombres ni documentos de identidad.",
            "El coordinador valida la capacidad instalada antes de aprobar una nueva entrega."
        ];

        var fragments = new List<DocumentSearchResult>();

        for (var index = 0; index < count; index++)
        {
            fragments.Add(TestSupport.Fragment(
                source: "misma-fuente",
                score: 0.9 - (index * 0.01),
                content: contents[index % contents.Length]));
        }

        return fragments;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
