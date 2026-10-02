namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>BC-01-IAM-and-UAM.md §11.1 <c>revoked_tokens</c> — a plain record, not a Domain aggregate (no behavior beyond existence).</summary>
public sealed class RevokedTokenRecord
{
    public string TokenIdOrRefreshHash { get; set; } = string.Empty;

    public DateTime RevokedOnUtc { get; set; }

    public DateTime ExpiresOnUtc { get; set; }
}
