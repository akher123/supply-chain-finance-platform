namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// A cross-module fact: <c>{ EventId: uuid, OccurredOnUtc: datetime }</c>
/// (Handover_Packages/00-Shared-Foundations.md §5). Every concrete integration event
/// lives in its owning module's <c>Contracts</c> project (the only layer other modules
/// may reference) and derives from this base. Integration events are never published
/// directly — they are written to <c>outbox_messages</c> in the same transaction as the
/// aggregate change and published by the outbox relay (§6.2). Once a shape is published
/// here it is a contract for the rest of the program: additive changes only, never a
/// silent breaking change (Stories/Event_Catalog.md conventions).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
