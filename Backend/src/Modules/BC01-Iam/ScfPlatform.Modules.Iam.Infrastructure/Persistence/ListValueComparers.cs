using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>Value comparers for the <c>json</c>-converted <c>list&lt;string&gt;</c> properties (§11.2) so EF Core's change tracker compares contents, not references.</summary>
public static class ListValueComparers
{
    public static ValueComparer<IReadOnlyList<string>> StringList { get; } = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        list => list.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
        list => list.ToList());
}
