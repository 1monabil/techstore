using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TechStore.Domain.Entities;

namespace TechStore.Infrastructure.Persistence;

/// <summary>
/// Writes an <see cref="OutboxMessage"/> for every product-affecting change as part of the same
/// SaveChanges call, so the message commits in the same transaction as the data. No repository
/// can forget to do it. Also stamps CreatedAt/UpdatedAt.
/// </summary>
public sealed class OutboxInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    internal void Capture(DbContext db)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var upserts = new HashSet<long>();
        var deletes = new HashSet<long>();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                // Entities are init-only; timestamps are written through the change tracker.
                case Product p when entry.State == EntityState.Added:
                    if (p.CreatedAt == default) entry.Property(nameof(Product.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(Product.UpdatedAt)).CurrentValue = now;
                    upserts.Add(p.Id);
                    break;
                case Product p when entry.State == EntityState.Modified:
                    entry.Property(nameof(Product.UpdatedAt)).CurrentValue = now;
                    upserts.Add(p.Id);
                    break;
                case Product p when entry.State == EntityState.Deleted:
                    deletes.Add(p.Id);
                    break;
                case ProductAttribute a when entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted:
                    upserts.Add(a.ProductId);
                    break;
                case ProductImage i when entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted:
                    upserts.Add(i.ProductId);
                    break;
            }
        }

        upserts.ExceptWith(deletes);
        upserts.Remove(0); // child of a product not yet keyed; the product's own entry covers it

        var set = db.Set<OutboxMessage>();
        foreach (var id in upserts)
            set.Add(new OutboxMessage { Type = OutboxMessageTypes.ProductUpserted, AggregateId = id, OccurredAt = now });
        foreach (var id in deletes)
            set.Add(new OutboxMessage { Type = OutboxMessageTypes.ProductDeleted, AggregateId = id, OccurredAt = now });
    }
}
