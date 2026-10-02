using ScfPlatform.BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ScfPlatform.BuildingBlocks.Infrastructure;

/// <summary>
/// After a unit of work is saved, collects the <see cref="DomainEvent"/>s from every changed
/// aggregate and dispatches them in-process, then clears them (Foundations §6.1). Runs in
/// <see cref="SavedChangesAsync"/> — i.e. only after the transaction has actually committed —
/// so a handler never reacts to a change that didn't happen.
///
/// Registered per module, once that module builds its <c>DbContext</c>:
/// <code>
/// services.AddDbContext&lt;MyModuleDbContext&gt;((sp, options) =&gt;
///     options.UseSqlServer(...).AddInterceptors(sp.GetRequiredService&lt;DomainEventDispatchInterceptor&gt;()));
/// </code>
/// </summary>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;

    public DomainEventDispatchInterceptor(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;

        if (context is not null)
        {
            await DispatchDomainEventsAsync(context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var entitiesWithEvents = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            var domainEvents = entity.DomainEvents.ToList();
            entity.ClearDomainEvents();

            foreach (var domainEvent in domainEvents)
            {
                await DynamicNotificationPublisher.PublishDomainEventAsync(_publisher, domainEvent, cancellationToken);
            }
        }
    }
}
