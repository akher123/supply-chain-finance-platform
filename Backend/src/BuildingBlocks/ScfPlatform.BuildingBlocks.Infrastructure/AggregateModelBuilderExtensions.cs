using ScfPlatform.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.BuildingBlocks.Infrastructure;

/// <summary>
/// EF Core's default model-discovery convention treats any public readable collection
/// property — including <see cref="AggregateRoot{TId}.DomainEvents"/>, inherited by every
/// aggregate from the shared kernel — as a navigation to another entity type, which then
/// fails model validation (no key on <see cref="DomainEvent"/>). Every module's own
/// <c>DbContext</c> should call this once, after configuring its entities, instead of each
/// rediscovering and working around the same EF convention quirk.
/// </summary>
public static class AggregateModelBuilderExtensions
{
    public static ModelBuilder IgnoreDomainEvents(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IHasDomainEvents).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(IHasDomainEvents.DomainEvents));
            }
        }

        return modelBuilder;
    }
}
