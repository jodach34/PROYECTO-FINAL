using Rescauta.Application.Features.Ai.Dto;

namespace Rescauta.Application.Features.Ai;

/// <summary>Acceso de solo lectura a la proyección pública del directorio de comedores.</summary>
public interface IChatbotPublicDataProvider
{
    Task<ChatbotDynamicDataDto> ObtenerDirectorioComedoresAsync(
        CancellationToken cancellationToken = default);
}
