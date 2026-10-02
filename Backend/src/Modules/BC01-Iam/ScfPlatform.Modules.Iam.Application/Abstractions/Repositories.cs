using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Application.Abstractions;

// Repository interfaces (BC-01-IAM-and-UAM.md §11.3) — defined in Application, implemented in
// Infrastructure against the module's own IamDbContext. Application never references the ORM.

public sealed record UserSearchCriteria(
    string? SearchText,
    UserRole? Role,
    AccountStatus? Status,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    bool? IdentityVerified,
    int Page,
    int PageSize);

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(UserAccountId id, CancellationToken cancellationToken);

    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<UserAccount?> GetByMobileAsync(string mobile, CancellationToken cancellationToken);

    /// <summary>Auto-detects whether <paramref name="identifier"/> is an email or a mobile number (§10.1 <c>LoginWithCredentialsCommand</c>).</summary>
    Task<UserAccount?> GetByEmailOrMobileAsync(string identifier, CancellationToken cancellationToken);

    Task<UserAccount?> GetBySessionRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken);

    Task<UserAccount?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> SearchAsync(UserSearchCriteria criteria, CancellationToken cancellationToken);

    Task AddAsync(UserAccount account, CancellationToken cancellationToken);

    void Update(UserAccount account);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task<bool> MobileExistsAsync(string mobile, CancellationToken cancellationToken);
}

public interface IOtpChallengeRepository
{
    Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken cancellationToken);

    /// <summary>The single <see cref="OtpStatus.Issued"/> challenge for this account/purpose, if any (§11.1's partial index mirrors this access pattern).</summary>
    Task<OtpChallenge?> GetActiveByAccountAndPurposeAsync(UserAccountId accountId, OtpPurpose purpose, CancellationToken cancellationToken);

    Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken);

    void Update(OtpChallenge challenge);
}

public interface IRevokedTokenStore
{
    Task AddAsync(string tokenIdOrRefreshHash, DateTime revokedOnUtc, DateTime expiresOnUtc, CancellationToken cancellationToken);

    Task<bool> IsRevokedAsync(string tokenIdOrRefreshHash, CancellationToken cancellationToken);
}

public sealed record AdminActionLogQuery(
    Guid? AdminUserId,
    Guid? TargetUserId,
    AdminActionType? ActionType,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public interface IAdminActionLogRepository
{
    Task AddAsync(AdminActionLog entry, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AdminActionLog> Items, int TotalCount)> QueryAsync(AdminActionLogQuery query, CancellationToken cancellationToken);
}

/// <summary>Persistence for the §8.3 AccountDeactivationCascade saga's tracking aggregate — internal to this module, never exposed via Contracts.</summary>
public interface IDeactivationCascadeRunRepository
{
    Task<DeactivationCascadeRun?> GetByUserIdAsync(UserAccountId userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeactivationCascadeRun>> GetInProgressAsync(CancellationToken cancellationToken);

    Task AddAsync(DeactivationCascadeRun run, CancellationToken cancellationToken);

    void Update(DeactivationCascadeRun run);
}
