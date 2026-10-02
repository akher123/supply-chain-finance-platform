namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// Base type for aggregate roots — the only entities application code is allowed to
/// load/save directly. Accumulates <see cref="DomainEvent"/>s raised by aggregate
/// behavior; a mediator pipeline step dispatches and clears them after the unit of
/// work commits (Handover_Packages/00-Shared-Foundations.md §5, §6.1).
/// </summary>
/// <typeparam name="TId">The aggregate's strongly-typed identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Optimistic-concurrency token (Foundations §6.4). Mapped by the ORM in Infrastructure;
    /// the Domain layer only increments/reads it, never queries the database.
    /// </summary>
    public int Version { get; protected set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
