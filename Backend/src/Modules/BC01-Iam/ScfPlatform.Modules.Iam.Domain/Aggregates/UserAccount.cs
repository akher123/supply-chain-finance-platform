using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Events;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Domain.Services;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;

namespace ScfPlatform.Modules.Iam.Domain.Aggregates;

/// <summary>
/// BC-01-IAM-and-UAM.md §5.1. The BC's aggregate root — <see cref="UserAccountId"/>'s wrapped
/// <c>Guid</c> IS the platform-wide UserId every other module stores as a plain <c>uuid</c>.
/// Split across two files (this one: identity/lifecycle/session behaviors §6.1; the other,
/// <c>UserAccount.MfaPasswordAdmin.cs</c>: MFA/password/admin behaviors) purely for
/// reviewability — one aggregate, one class, `partial` only for file size.
/// </summary>
public sealed partial class UserAccount : AggregateRoot<UserAccountId>
{
    public static readonly TimeSpan DefaultSessionInactivityWindow = TimeSpan.FromHours(1);
    public static readonly TimeSpan RememberMeSessionTtl = TimeSpan.FromDays(14);
    public const int OtpFailureLockThreshold15Min = 15;

    private readonly List<Session> _sessions = [];
    private readonly List<TrustedDevice> _trustedDevices = [];
    private readonly List<BackupCode> _backupCodes = [];
    private readonly List<PasswordResetToken> _passwordResetTokens = [];
    private readonly List<string> _passwordHistory = [];
    private readonly List<string> _permissions = [];

    // ORM materialization only.
    private UserAccount()
    {
    }

    private UserAccount(UserAccountId id, Credential credential, UserRole role, DateTime nowUtc)
        : base(id)
    {
        Credential = credential;
        Role = role;
        Status = AccountStatus.PendingActivation;
        LockState = LockState.None;
        Mfa = MfaConfiguration.Disabled;
        IdentityVerified = false;
        CreatedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;
        _permissions.AddRange(PermissionResolver.Resolve(role));
    }

    public Credential Credential { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public AccountStatus Status { get; private set; }

    public LockState LockState { get; private set; } = LockState.None;

    public MfaConfiguration Mfa { get; private set; } = MfaConfiguration.Disabled;

    public IReadOnlyList<BackupCode> BackupCodes => _backupCodes.AsReadOnly();

    public IReadOnlyList<TrustedDevice> TrustedDevices => _trustedDevices.AsReadOnly();

    public IReadOnlyList<Session> Sessions => _sessions.AsReadOnly();

    public IReadOnlyList<PasswordResetToken> PasswordResetTokens => _passwordResetTokens.AsReadOnly();

    /// <summary>Last 3 password hashes — checked on change/reset to prevent reuse (§5.1, §6.2 invariant #4).</summary>
    public IReadOnlyList<string> PasswordHistory => _passwordHistory.AsReadOnly();

    /// <summary>Always a pure function of <see cref="Role"/> plus explicit admin grants — recomputed on <c>AssignRole</c> (§6.2 invariant #10). Never set independently.</summary>
    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    public bool IdentityVerified { get; private set; }

    public DateTime? ActivatedOnUtc { get; private set; }

    public string? SuspendedReason { get; private set; }

    public DateTime? DeactivatedOnUtc { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    /// <summary>
    /// §6.1. Creates the account in <see cref="AccountStatus.PendingActivation"/>. Uniqueness is
    /// checked by the *handler* against the repository before calling this (the DB unique index
    /// is the backstop, §6.2 invariant #2) — this factory does not re-check it. Permissions are
    /// derived from <paramref name="role"/> via <see cref="PermissionResolver"/>.
    /// </summary>
    public static Result<UserAccount> Provision(EmailAddress email, MobileNumber mobile, PasswordHash passwordHash, UserRole role, DateTime nowUtc)
    {
        var credentialResult = Credential.Create(email, mobile, passwordHash);

        if (credentialResult.IsFailure)
        {
            return Result.Failure<UserAccount>(credentialResult.Error);
        }

        return Result.Success(new UserAccount(UserAccountId.New(), credentialResult.Value, role, nowUtc));
    }

    /// <summary>§6.1. Only legal from <see cref="AccountStatus.PendingActivation"/>. Idempotent if already <see cref="AccountStatus.Active"/>; fails from <see cref="AccountStatus.Suspended"/>/<see cref="AccountStatus.Deactivated"/> (a narrower rule than the general state machine — see <c>ReactivateAfterDeactivation</c> for the <c>Deactivated → Active</c> path).</summary>
    public Result Activate(DateTime nowUtc)
    {
        if (Status == AccountStatus.Active)
        {
            return Result.Success();
        }

        if (Status != AccountStatus.PendingActivation)
        {
            return Result.Failure(Error.Conflict("E-ACCOUNT-NOT-PENDING-ACTIVATION", $"Cannot activate an account in status '{Status}'."));
        }

        var transition = AccountStateMachine.EnsureTransitionAllowed(Status, AccountStatus.Active);

        if (transition.IsFailure)
        {
            return transition;
        }

        Status = AccountStatus.Active;
        ActivatedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>§6.1, §6.2 invariant #7. Only legal from <see cref="AccountStatus.Active"/> — the handler is responsible for surfacing the matching distinct <c>E-LOGIN-ACCOUNT-*</c> code before ever reaching this call; this is a defensive guard, not the primary source of that codes' logic.</summary>
    public Result RecordSuccessfulLogin(
        SessionChannel channel,
        DeviceFingerprint deviceFingerprint,
        string refreshTokenHash,
        DateTime nowUtc,
        bool rememberMe)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(Error.Conflict("E-ACCOUNT-NOT-ACTIVE", $"Cannot record a successful login for an account in status '{Status}'."));
        }

        var ttl = rememberMe ? RememberMeSessionTtl : DefaultSessionInactivityWindow;
        var session = new Session(SessionId.New(), channel, deviceFingerprint, refreshTokenHash, nowUtc, nowUtc.Add(ttl));
        _sessions.Add(session);

        LockState = LockState.WithFailedLoginCount(0);
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>
    /// §6.1. Increments <see cref="LockState.FailedLoginCount"/> for audit/diagnostics — per-IP
    /// throttling (10/min) is the *handler's* job via <c>RateLimiterPort</c> (§9.2), not this
    /// counter. The <c>identifier</c>/<c>reason</c> in BC-01's neutral method signature only feed
    /// the handler-constructed <c>UserLoginFailedIntegrationEvent</c> (which carries the raw
    /// identifier, never a UserId — login can fail before identification) — they are not account
    /// state, so this method takes no parameters.
    /// </summary>
    public void RecordFailedLogin(DateTime nowUtc)
    {
        LockState = LockState.WithFailedLoginCount(LockState.FailedLoginCount + 1);
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>§6.1. Adds a <see cref="TrustedDevice"/>; replaces any existing one with the same fingerprint.</summary>
    public void TrustDevice(DeviceFingerprint fingerprint, string label, DateTime untilUtc)
    {
        _trustedDevices.RemoveAll(device => device.DeviceFingerprint.Equals(fingerprint));
        _trustedDevices.Add(new TrustedDevice(TrustedDeviceId.New(), fingerprint, label, untilUtc));
    }

    /// <summary>§6.1. Marks the session's refresh token dead. Idempotent — revoking an already-revoked session succeeds without raising a second event.</summary>
    public Result RevokeSession(SessionId sessionId, DateTime nowUtc, string reason = "explicit")
    {
        var session = _sessions.SingleOrDefault(s => s.Id == sessionId);

        if (session is null)
        {
            return Result.Failure(Error.NotFound("E-SESSION-NOT-FOUND", "No such session on this account."));
        }

        if (!session.IsActive)
        {
            return Result.Success();
        }

        session.Revoke(nowUtc);
        Raise(new SessionRevokedDomainEvent(sessionId, Id, reason));
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>§6.1. Logout-everywhere; also called internally on password change/reset and on suspend/deactivate. Returns the ids actually revoked (already-revoked sessions are skipped) so the caller can build one integration event per session.</summary>
    public IReadOnlyList<SessionId> RevokeAllSessions(DateTime nowUtc, string reason)
    {
        var revoked = new List<SessionId>();

        foreach (var session in _sessions.Where(s => s.IsActive))
        {
            session.Revoke(nowUtc);
            Raise(new SessionRevokedDomainEvent(session.Id, Id, reason));
            revoked.Add(session.Id);
        }

        if (revoked.Count > 0)
        {
            UpdatedOnUtc = nowUtc;
        }

        return revoked;
    }

    /// <summary>§10.1 <c>RefreshAccessTokenCommand</c> — one-time-use refresh-token rotation: replaces the session's refresh-token hash and extends its expiry.</summary>
    public Result RotateSessionRefreshToken(SessionId sessionId, string newRefreshTokenHash, DateTime newExpiresOnUtc, DateTime nowUtc)
    {
        var session = _sessions.SingleOrDefault(s => s.Id == sessionId && s.IsActive);

        if (session is null)
        {
            return Result.Failure(Error.NotFound("E-SESSION-NOT-FOUND", "No such active session on this account."));
        }

        session.Rotate(newRefreshTokenHash, newExpiresOnUtc);
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>§6.1, §6.2 invariant #14. Updates <see cref="Session.LastSeenUtc"/>; if the inactivity window has elapsed, revokes the session instead. Returns whether it timed out.</summary>
    public Result<bool> TouchSession(SessionId sessionId, DateTime nowUtc, TimeSpan? inactivityWindow = null)
    {
        var session = _sessions.SingleOrDefault(s => s.Id == sessionId && s.IsActive);

        if (session is null)
        {
            return Result.Failure<bool>(Error.NotFound("E-SESSION-NOT-FOUND", "No such active session on this account."));
        }

        var window = inactivityWindow ?? DefaultSessionInactivityWindow;

        if (nowUtc - session.LastSeenUtc > window)
        {
            session.Revoke(nowUtc);
            Raise(new SessionRevokedDomainEvent(sessionId, Id, "idle-timeout"));
            UpdatedOnUtc = nowUtc;

            return Result.Success(true);
        }

        session.Touch(nowUtc);
        UpdatedOnUtc = nowUtc;

        return Result.Success(false);
    }

    /// <summary>
    /// The module's session-expiry sweep job (§3) calls this instead of <see cref="TouchSession"/>
    /// — a sweep isn't "this session was just used", it's "is this session past its inactivity
    /// *or* absolute expiry" (§3: "marks sessions past their inactivity/absolute expiry"). Revokes
    /// every active session matching either criterion and returns their ids.
    /// </summary>
    public IReadOnlyList<SessionId> ExpireStaleSessions(DateTime nowUtc, TimeSpan inactivityWindow)
    {
        var expired = new List<SessionId>();

        foreach (var session in _sessions.Where(s => s.IsActive))
        {
            var pastAbsoluteExpiry = nowUtc > session.ExpiresOnUtc;
            var pastInactivityWindow = nowUtc - session.LastSeenUtc > inactivityWindow;

            if (!pastAbsoluteExpiry && !pastInactivityWindow)
            {
                continue;
            }

            session.Revoke(nowUtc);
            Raise(new SessionRevokedDomainEvent(session.Id, Id, "idle-timeout"));
            expired.Add(session.Id);
        }

        if (expired.Count > 0)
        {
            UpdatedOnUtc = nowUtc;
        }

        return expired;
    }

    /// <summary>§6.1. Sets the transient lockout flag with an auto-expiry.</summary>
    public void Lock(DateTime untilUtc, DateTime nowUtc)
    {
        LockState = LockState.Locked(untilUtc);
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>§6.1. Clears the transient lockout flag and resets both failed-attempt counters.</summary>
    public void Unlock(DateTime nowUtc)
    {
        LockState = LockState.Unlocked();
        UpdatedOnUtc = nowUtc;
        Raise(new AccountUnlockedDomainEvent(Id));
    }

    /// <summary>§6.1, §6.2 invariant #5. Increments <see cref="LockState.FailedOtpCount"/>; at <paramref name="purposeMaxAttempts"/> the account locks for 15 minutes.</summary>
    public void RecordOtpFailure(int purposeMaxAttempts, DateTime nowUtc)
    {
        var newCount = LockState.FailedOtpCount + 1;
        LockState = LockState.WithFailedOtpCount(newCount);
        UpdatedOnUtc = nowUtc;

        if (newCount >= purposeMaxAttempts)
        {
            Lock(nowUtc.AddMinutes(OtpFailureLockThreshold15Min), nowUtc);
        }
    }
}
