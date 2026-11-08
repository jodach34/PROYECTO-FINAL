using Microsoft.EntityFrameworkCore;
using Rescauta.Application.Features.Ai.Dto;
using Rescauta.Application.Interfaces;
using Rescauta.Domain.Entities.Comedores;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Lee únicamente los campos públicos necesarios para el directorio del chatbot.
/// El filtro global de AppDbContext excluye comedores eliminados lógicamente.
/// </summary>
public sealed class ChatbotPublicDataProvider(IAppDbContext dbContext)
    : IChatbotPublicDataProvider
{
    public const int MaxComedores = 20;

    public async Task<ChatbotDynamicDataDto> ObtenerDirectorioComedoresAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var query = dbContext.Set<Comedor>().AsNoTracking();

        var total = await query.CountAsync(cancellationToken);

        var comedores = await query
            .OrderBy(comedor => comedor.Nombre)
            .ThenBy(comedor => comedor.Id)
            .Select(comedor => new ChatbotPublicComedorDto
            {
                Nombre = comedor.Nombre,
                Distrito = comedor.Distrito
            })
            .Take(MaxComedores)
            .ToListAsync(cancellationToken);

        var distritos = await query
            .Select(comedor => comedor.Distrito)
            .Distinct()
            .OrderBy(distrito => distrito)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ChatbotDynamicDataDto
        {
            TotalComedores = total,
            Comedores = comedores,
            Distritos = distritos
        };
    }
}
