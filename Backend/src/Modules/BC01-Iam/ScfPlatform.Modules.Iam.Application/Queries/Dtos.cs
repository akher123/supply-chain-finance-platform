using ScfPlatform.Modules.Iam.Domain.Enums;

namespace ScfPlatform.Modules.Iam.Application.Queries;

// DTOs (BC-01-IAM-and-UAM.md §10.4). Never a password hash / OTP code / refresh-token hash / MFA
// secret / backup-code hash. Mobile numbers are masked in admin/list DTOs (and here, in the
// self-view too — §10.2 lists "mobile masked" directly on AccountDto).

public sealed record AccountDto(Guid UserId, string Email, string MobileMasked, UserRole Role, AccountStatus Status, bool MfaEnabled, bool IdentityVerified);

public sealed record SessionDto(Guid SessionId, SessionChannel Channel, string DeviceLabel, DateTime IssuedOnUtc, DateTime LastSeenUtc, bool IsCurrent);

public sealed record MfaStatusDto(bool Enabled, MfaMethod Method, DateTime? LastVerifiedOnUtc, int BackupCodesRemaining);

public sealed record UserListItemDto(Guid UserId, string Email, string MobileMasked, UserRole Role, AccountStatus Status, DateTime CreatedOnUtc, bool IdentityVerified);

public sealed record AdminUserDetailDto(
    Guid UserId,
    string Email,
    string MobileMasked,
    UserRole Role,
    AccountStatus Status,
    DateTime CreatedOnUtc,
    bool IdentityVerified,
    bool IsLocked,
    DateTime? LockedUntilUtc,
    int ActiveSessionCount);

public sealed record AdminActionDto(Guid Id, Guid AdminUserId, AdminActionType ActionType, Guid TargetUserId, string? Reason, DateTime OccurredOnUtc);
