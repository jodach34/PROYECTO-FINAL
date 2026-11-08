namespace Rescauta.Application.Features.Ai.Dto;

/// <summary>Datos públicos mínimos usados para responder preguntas sobre comedores.</summary>
public sealed record ChatbotDynamicDataDto
{
    public int TotalComedores { get; init; }

    public IReadOnlyList<ChatbotPublicComedorDto> Comedores { get; init; } = [];

    public IReadOnlyList<string> Distritos { get; init; } = [];
}

/// <summary>Proyección pública; deliberadamente no contiene ubicación ni datos operativos.</summary>
public sealed record ChatbotPublicComedorDto
{
    public string Nombre { get; init; } = string.Empty;

    public string Distrito { get; init; } = string.Empty;
}
