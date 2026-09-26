using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Rescauta.Domain.Common;

namespace Rescauta.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Rellena automaticamente los campos de auditoria de <see cref="BaseEntity"/>
/// (CreatedAt / CreatedBy / UpdatedAt / UpdatedBy) en cada SaveChanges.
///
/// Motivo: son 3 devs escribiendo 3 modulos. Si la auditoria se completa a mano en cada
/// caso de uso, tarde o temprano falta un campo. Esto lo resuelve una sola vez para todos.
/// </summary>
public sealed class AuditableEntityInterceptor(IHttpContextAccessor? httpContextAccessor = null)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = httpContextAccessor?.HttpContext?.User.Identity?.Name;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user ?? entry.Entity.CreatedBy;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    break;
            }
        }
    }
}
