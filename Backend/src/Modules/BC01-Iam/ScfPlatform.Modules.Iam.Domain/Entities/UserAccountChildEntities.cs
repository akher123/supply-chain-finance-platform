using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;

namespace ScfPlatform.Modules.Iam.Domain.Entities;

// Child entities of the UserAccount aggregate (BC-01-IAM-and-UAM.md §5.1) — identity local to the
// aggregate; constructors/mutators are `internal` so they can only be created/mutated from within
// this assembly, i.e. by UserAccount itself (the shared-kernel rule: "only mutated through the root").

/// <summary>One per successful login (§2, §5.1). <see cref="LastSeenUtc"/> drives the inactivity timeout; <see cref="ExpiresOnUtc"/> is extended to 14 days when "remember me" is set.</summary>
public sealed class Session : Entity<SessionId>
{
    private Session()
    {
    }

    internal Session(
        SessionId id,
        SessionChannel channel,
        DeviceFingerprint deviceFingerprint,
        string refreshTokenHash,
        DateTime issuedOnUtc,
        DateTime expiresOnUtc)
        : base(id)
    {
        Channel = channel;
        DeviceFingerprint = deviceFingerprint;
        RefreshTokenHash = refreshTokenHash;
        IssuedOnUtc = issuedOnUtc;
        LastSeenUtc = issuedOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        RevokedOnUtc = null;
    }

    public SessionChannel Channel { get; private set; }

    public DeviceFingerprint DeviceFingerprint { get; private set; } = null!;

    public string RefreshTokenHash { get; private set; } = string.Empty;

    public DateTime IssuedOnUtc { get; private set; }

    public DateTime LastSeenUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? RevokedOnUtc { get; private set; }

    public bool IsActive => RevokedOnUtc is null;

    internal void Touch(DateTime nowUtc) => LastSeenUtc = nowUtc;

    internal void Revoke(DateTime nowUtc) => RevokedOnUtc ??= nowUtc;

    internal void Rotate(string newRefreshTokenHash, DateTime newExpiresOnUtc)
    {
        RefreshTokenHash = newRefreshTokenHash;
        ExpiresOnUtc = newExpiresOnUtc;
    }
}

/// <summary>A device fingerprint remembered for diagnostics (§5.1). Does not bypass the MFA challenge (§6.2 invariant #13).</summary>
public sealed class TrustedDevice : Entity<TrustedDeviceId>
{
    private TrustedDevice()
    {
    }

    internal TrustedDevice(TrustedDeviceId id, DeviceFingerprint deviceFingerprint, string label, DateTime trustedUntilUtc)
        : base(id)
    {
        DeviceFingerprint = deviceFingerprint;
        Label = label;
        TrustedUntilUtc = trustedUntilUtc;
    }

    public DeviceFingerprint DeviceFingerprint { get; private set; } = null!;

    public string Label { get; private set; } = string.Empty;

    public DateTime TrustedUntilUtc { get; private set; }
}

/// <summary>One of the 8–10 single-use MFA recovery codes issued at setup (§2, §5.1). Stored hashed; a code with <see cref="UsedOnUtc"/> set cannot be redeemed again.</summary>
public sealed class BackupCode : Entity<BackupCodeId>
{
    private BackupCode()
    {
    }

    internal BackupCode(BackupCodeId id, string codeHash)
        : base(id)
    {
        CodeHash = codeHash;
        UsedOnUtc = null;
    }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTime? UsedOnUtc { get; private set; }

    public bool IsUsed => UsedOnUtc is not null;

    internal bool TryRedeem(string submittedCodeHash, DateTime nowUtc)
    {
        if (IsUsed || !string.Equals(CodeHash, submittedCodeHash, StringComparison.Ordinal))
        {
            return false;
        }

        UsedOnUtc = nowUtc;
        return true;
    }
}

/// <summary>At most one *unused, unexpired* token per account at a time (§5.1, §6.2 invariant #6).</summary>
public sealed class PasswordResetToken : Entity<PasswordResetTokenId>
{
    private PasswordResetToken()
    {
    }

    internal PasswordResetToken(PasswordResetTokenId id, string tokenHash, DateTime issuedOnUtc, DateTime expiresOnUtc)
        : base(id)
    {
        TokenHash = tokenHash;
        IssuedOnUtc = issuedOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        UsedOnUtc = null;
    }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime IssuedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? UsedOnUtc { get; private set; }

    public bool IsUsable(string submittedTokenHash, DateTime nowUtc) =>
        UsedOnUtc is null
        && nowUtc <= ExpiresOnUtc
        && string.Equals(TokenHash, submittedTokenHash, StringComparison.Ordinal);

    internal void MarkUsed(DateTime nowUtc) => UsedOnUtc ??= nowUtc;
}
