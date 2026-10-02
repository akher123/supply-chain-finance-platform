namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// Base type for every domain entity. Equality is by identity (<see cref="Id"/>), never by
/// component values. See Handover_Packages/00-Shared-Foundations.md §5.
/// </summary>
/// <typeparam name="TId">The entity's strongly-typed identifier type.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Reserved for ORM materialization only — never call from application code.
    /// </summary>
    protected Entity()
    {
    }

    public TId Id { get; protected set; } = default!;

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
