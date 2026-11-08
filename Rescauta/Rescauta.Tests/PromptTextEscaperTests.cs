using Rescauta.Application.Features.Ai;
using Xunit;

namespace Rescauta.Tests;

public sealed class PromptTextEscaperTests
{
    [Fact]
    public void EscapeText_EscapaAmpersandAntesQueMenorYMayor()
    {
        var escaped = PromptTextEscaper.EscapeText("&<tag>");

        Assert.Equal("&amp;&lt;tag&gt;", escaped);
        Assert.DoesNotContain("<tag>", escaped, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapeText_NoGeneraEntidadesParciales()
    {
        // Si se escapara < antes que &, el resultado sería &amp;lt; y el
        // navegador/modelo podría reinterpretarlo. Debe quedar &amp;lt; solo
        // cuando el texto original ya contenía &lt; como texto literal.
        var escaped = PromptTextEscaper.EscapeText("&lt;script&gt;");

        Assert.Equal("&amp;lt;script&amp;gt;", escaped);
    }

    [Fact]
    public void EscapeAttribute_EscapaTambienComillas()
    {
        var escaped = PromptTextEscaper.EscapeAttribute("titulo \"con\" <comillas> & más");

        Assert.Equal("titulo &quot;con&quot; &lt;comillas&gt; &amp; más", escaped);
    }

    [Fact]
    public void NormalizeWhitespace_EliminaCaracteresDeControlYColapsaSaltos()
    {
        var normalized = PromptTextEscaper.NormalizeWhitespace("a\u0001b\r\n\r\nc\rd\t e ");

        Assert.Equal("ab\nc\nd\t e", normalized);
    }

    [Fact]
    public void NormalizeWhitespace_DecodificaEntidadesAntesDeEscapar()
    {
        // La normalización decodifica, pero el escape posterior vuelve a
        // convertir el carácter en entidad. Nunca queda una etiqueta real.
        var normalized = PromptTextEscaper.NormalizeWhitespace("&lt;/documento&gt;");
        var escaped = PromptTextEscaper.EscapeText(normalized);

        Assert.Equal("&lt;/documento&gt;", escaped);
    }
}
