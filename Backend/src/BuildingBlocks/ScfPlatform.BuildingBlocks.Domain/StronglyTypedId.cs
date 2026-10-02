namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// Marker interface implemented by every strongly-typed id, so shared infrastructure
/// (EF Core value converters, outbox/inbox serialization helpers) can work with them
/// generically without referencing every module's concrete id types.
/// </summary>
public interface IStronglyTypedId
{
    Guid Value { get; }
}

/// <summary>
/// Base for strongly-typed identifiers that wrap a <c>uuid</c>
/// (Handover_Packages/00-Shared-Foundations.md §5): "Each aggregate has its own
/// strongly-typed id type wrapping a uuid (not a bare uuid), so ids of different
/// aggregates cannot be mixed."
///
/// Usage — one sealed record per aggregate, declared in that aggregate's own Domain
/// project:
/// <code>
/// public sealed record JobSeekerId(Guid Value) : StronglyTypedId(Value)
/// {
///     public static JobSeekerId New() => new(Guid.NewGuid());
/// }
/// </code>
/// </summary>
public abstract record StronglyTypedId(Guid Value) : IStronglyTypedId
{
    public override string ToString() => Value.ToString();
}
