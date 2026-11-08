using Microsoft.Extensions.Options;
using Rescauta.Application.Features.Ai.Models;

namespace Rescauta.Application.Features.Ai;

/// <summary>
/// Limita las generaciones simultáneas contra Gemini para proteger la cuota del
/// servidor. Es singleton: el límite es global para la instancia de la API.
/// </summary>
public sealed class RagConcurrencyGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public RagConcurrencyGate(IOptions<AiRagOptions> options)
    {
        var limit = Math.Max(1, options.Value.MaxConcurrentGenerations);
        _semaphore = new SemaphoreSlim(limit, limit);
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => _semaphore.WaitAsync(timeout, cancellationToken);

    public void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}
