using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sovva.Application.Helpers;

namespace Sovva.Infrastructure.Data;

/// <summary>
/// EF Core interceptor that automatically:
/// 1. Sets CreatedAt and UpdatedAt timestamps on save.
/// 2. Converts hard DELETEs into soft deletes for any entity that
///    implements ISoftDeletable, by setting DeletedAt instead.
/// </summary>
public sealed class TimestampInterceptor : SaveChangesInterceptor
{
    private readonly IAppTimeProvider _time;

    public TimestampInterceptor(IAppTimeProvider time) => _time = time;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateTimestamps(DbContext? context)
    {
        if (context is null) return;

        var now = _time.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // ── Soft-delete interception ──────────────────────────────────────
            // If any ISoftDeletable entity is being hard-deleted, convert it to
            // a soft delete by marking it Modified and stamping DeletedAt.
            if (entry.State == EntityState.Deleted &&
                entry.Entity is Sovva.Domain.Interfaces.ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.DeletedAt = now;

                // Also stamp UpdatedAt so auditing is consistent
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;

                continue; // Skip the timestamp block below — already handled
            }

            // ── Normal timestamp management ───────────────────────────────────
            if (entry.State == EntityState.Added)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
                    entry.Property("CreatedAt").CurrentValue = now;

                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;
            }

            if (entry.State == EntityState.Modified)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedAt"))
                    entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
