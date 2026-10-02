using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>
/// Value objects (or their fields) that don't need to be queried/indexed map to <c>json</c>
/// columns (00-Shared-Foundations.md §6.4) — <c>password_history</c>, <c>permissions</c>,
/// <c>lock_state</c>, <c>mfa</c> (§11.2).
/// </summary>
public static class JsonValueConverters
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static ValueConverter<IReadOnlyList<string>, string> StringList { get; } = new(
        list => JsonSerializer.Serialize(list, Options),
        json => JsonSerializer.Deserialize<List<string>>(json, Options) ?? new List<string>());
}
