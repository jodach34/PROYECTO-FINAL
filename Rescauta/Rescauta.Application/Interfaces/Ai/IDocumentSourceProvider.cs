using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Interfaces.Ai;

/// <summary>
/// Entrega los documentos institucionales curados que deben indexarse.
/// La implementación concreta vive en Infrastructure y no debe leer datos personales.
/// </summary>
public interface IDocumentSourceProvider
{
    IReadOnlyList<DocumentSource> GetDocuments();
}
