namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// Non-generic view of <see cref="AggregateRoot{TId}"/> so cross-cutting infrastructure
/// (the EF Core save-changes interceptor that dispatches domain events, Foundations §6.1)
/// can enumerate changed aggregates via <c>ChangeTracker</c> without a generic type
/// parameter per aggregate type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
