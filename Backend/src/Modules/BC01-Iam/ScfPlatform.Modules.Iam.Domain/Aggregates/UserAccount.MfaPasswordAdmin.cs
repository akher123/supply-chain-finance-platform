using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Events;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using ScfPlatform.Modules.Iam.Domain.Services;

namespace ScfPlatform.Modules.Iam.Domain.Aggregates;

/// <summary>MFA, password, and admin-action behaviors (BC-01-IAM-and-UAM.md §6.1) — see <c>UserAccount.cs</c> for identity/lifecycle/session behaviors and the shared aggregate state.</summary>
public sealed partial class UserAccount
{
    public const int MinimumBackupCodes = 8;
    public const int MaximumBackupCodes = 10;
    public const int PasswordHistoryDepth = 3;
    public static readonly TimeSpan DeactivationRetentionWindow = TimeSpan.FromDays(365);

    /// <summary>§6.1. Only from <see cref="AccountStatus.Active"/>, and only after a verify-before-activate confirmation step (handler-driven, §10.1 <c>ConfirmMfaEnrollmentCommand</c>). Stores 8–10 backup-code hashes.</summary>
    public Result EnableMfa(MfaMethod method, string secretRef, IReadOnlyList<string> backupCodeHashes, DateTime nowUtc)
    {
        if (Status != AccountStatus.Active)
        {
            return Result.Failure(Error.Conflict("E-ACCOUNT-NOT-ACTIVE", "MFA can only be enabled on an active account."));
        }

        if (backupCodeHashes.Count is < MinimumBackupCodes or > MaximumBackupCodes)
        {
            return Result.Failure(Error.Validation(
                "E-MFA-BACKUP-CODES-INVALID",
                $"Expected between {MinimumBackupCodes} and {MaximumBackupCodes} backup codes."));
        }

        var mfaResult = MfaConfiguration.Create(true, method, secretRef);

        if (mfaResult.IsFailure)
        {
            return Result.Failure(mfaResult.Error);
        }

        Mfa = mfaResult.Value;
        _backupCodes.Clear();
        _backupCodes.AddRange(backupCodeHashes.Select(hash => new BackupCode(BackupCodeId.New(), hash)));
        UpdatedOnUtc = nowUtc;

        Raise(new MfaEnabledDomainEvent(Id, method));

        return Result.Success();
    }

    /// <summary>§6.1. Clears MFA config and backup codes. The caller having re-authenticated is a handler concern, not enforced here.</summary>
    public void DisableMfa(DateTime nowUtc)
    {
        Mfa = MfaConfiguration.Disabled;
        _backupCodes.Clear();
        UpdatedOnUtc = nowUtc;

        Raise(new MfaDisabledDomainEvent(Id));
    }

    /// <summary>§6.1. The code must exist and be unused; marks it used. Allows login to proceed in place of the second factor.</summary>
    public Result RedeemBackupCode(string submittedCodeHash, DateTime nowUtc)
    {
        var backupCode = _backupCodes.FirstOrDefault(code => !code.IsUsed && code.TryRedeem(submittedCodeHash, nowUtc));

        if (backupCode is null)
        {
            return Result.Failure(Error.Validation("E-MFA-BACKUP-CODE-INVALID", "This backup code is invalid or already used."));
        }

        UpdatedOnUtc = nowUtc;
        Raise(new BackupCodeRedeemedDomainEvent(Id, backupCode.Id));

        return Result.Success();
    }

    /// <summary>§6.1, §6.2 invariant #6. Invalidates any prior unused token before adding the new one — at most one active token per account.</summary>
    public void IssuePasswordResetToken(string tokenHash, DateTime nowUtc, DateTime expiresOnUtc)
    {
        foreach (var priorToken in _passwordResetTokens.Where(token => token.UsedOnUtc is null))
        {
            priorToken.MarkUsed(nowUtc);
        }

        _passwordResetTokens.Add(new PasswordResetToken(PasswordResetTokenId.New(), tokenHash, nowUtc, expiresOnUtc));
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>§6.1, §6.2 invariants #4/#8. Token must exist/be unused/unexpired/match; new hash must not repeat the last <see cref="PasswordHistoryDepth"/> hashes; revokes every session; clears <see cref="LockState"/>. Distinguishes an unknown/already-used token (<c>E-RESET-TOKEN-INVALID</c>) from an otherwise-matching but expired one (<c>E-RESET-TOKEN-EXPIRED</c>) — §12 documents both as separate codes on the same route.</summary>
    public Result CompletePasswordReset(string tokenHash, PasswordHash newPasswordHash, DateTime nowUtc)
    {
        var token = _passwordResetTokens.FirstOrDefault(t => t.UsedOnUtc is null && string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal));

        if (token is null)
        {
            return Result.Failure(Error.Validation("E-RESET-TOKEN-INVALID", "This password-reset token is invalid or already used."));
        }

        if (nowUtc > token.ExpiresOnUtc)
        {
            return Result.Failure(Error.Validation("E-RESET-TOKEN-EXPIRED", "This password-reset token has expired."));
        }

        var reuseCheck = EnsureNotPasswordReuse(newPasswordHash);

        if (reuseCheck.IsFailure)
        {
            return reuseCheck;
        }

        ApplyNewPasswordHash(newPasswordHash);
        token.MarkUsed(nowUtc);
        RevokeAllSessions(nowUtc, "password-change");
        LockState = LockState.Unlocked();
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>§6.1, §6.2 invariants #4/#8. Self-service while authenticated. New hash must not repeat the last <see cref="PasswordHistoryDepth"/> hashes; revokes every session.</summary>
    public Result ChangePassword(PasswordHash newPasswordHash, DateTime nowUtc)
    {
        var reuseCheck = EnsureNotPasswordReuse(newPasswordHash);

        if (reuseCheck.IsFailure)
        {
            return reuseCheck;
        }

        ApplyNewPasswordHash(newPasswordHash);
        RevokeAllSessions(nowUtc, "password-change");
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    private Result EnsureNotPasswordReuse(PasswordHash newPasswordHash)
    {
        if (_passwordHistory.Contains(newPasswordHash.Value, StringComparer.Ordinal)
            || string.Equals(Credential.PasswordHash.Value, newPasswordHash.Value, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation("E-RESET-PASSWORD-REUSED", $"New password must not match any of the last {PasswordHistoryDepth} passwords."));
        }

        return Result.Success();
    }

    private void ApplyNewPasswordHash(PasswordHash newPasswordHash)
    {
        _passwordHistory.Insert(0, Credential.PasswordHash.Value);

        while (_passwordHistory.Count > PasswordHistoryDepth)
        {
            _passwordHistory.RemoveAt(_passwordHistory.Count - 1);
        }

        Credential.ReplacePasswordHash(newPasswordHash);
    }

    /// <summary>§6.1, §6.2 invariant #10. Recomputes <see cref="Permissions"/> via <see cref="PermissionResolver"/>. Only an <see cref="UserRole.MoLAdministrator"/> may be the grantor — handler-checked, not enforced here.</summary>
    public void AssignRole(UserRole role, DateTime nowUtc)
    {
        Role = role;
        _permissions.Clear();
        _permissions.AddRange(PermissionResolver.Resolve(role));
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>§6.1, §6.2 invariant #11. Only from <see cref="AccountStatus.Active"/> (or <see cref="AccountStatus.PendingActivation"/> for the employer-reject path). Requires a non-empty <paramref name="reason"/>. Not self-reversible — only <c>Reinstate</c> recovers it.</summary>
    public Result Suspend(string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("E-SUSPEND-REASON-REQUIRED", "A reason is required to suspend an account."));
        }

        var transition = AccountStateMachine.EnsureTransitionAllowed(Status, AccountStatus.Suspended);

        if (transition.IsFailure)
        {
            return transition;
        }

        Status = AccountStatus.Suspended;
        SuspendedReason = reason;
        UpdatedOnUtc = nowUtc;

        // §6.2 invariant #8 — the Contracts UserLoggedOutIntegrationEvent.Reason enum has no
        // bespoke "suspended"/"deactivated" value; "logout-all" is the closest documented fit
        // for a bulk, non-self-initiated revocation (see BUILD_REPORT.md for the write-up).
        RevokeAllSessions(nowUtc, "logout-all");

        return Result.Success();
    }

    /// <summary>§6.1. Only from <see cref="AccountStatus.Suspended"/>. Clears <see cref="SuspendedReason"/>.</summary>
    public Result Reinstate(DateTime nowUtc)
    {
        var transition = AccountStateMachine.EnsureTransitionAllowed(Status, AccountStatus.Active);

        if (transition.IsFailure)
        {
            return transition;
        }

        Status = AccountStatus.Active;
        SuspendedReason = null;
        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>§6.1, §6.2 invariant #8. Only from <see cref="AccountStatus.Active"/>. A soft delete — used both by self-deactivation and admin soft-delete.</summary>
    public Result Deactivate(DateTime nowUtc)
    {
        var transition = AccountStateMachine.EnsureTransitionAllowed(Status, AccountStatus.Deactivated);

        if (transition.IsFailure)
        {
            return transition;
        }

        Status = AccountStatus.Deactivated;
        DeactivatedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;

        RevokeAllSessions(nowUtc, "logout-all");

        return Result.Success();
    }

    /// <summary>
    /// §6.1. Only from <see cref="AccountStatus.Deactivated"/> and within the 12-month retention
    /// window (<see cref="DeactivationRetentionWindow"/>). Goes to <see cref="AccountStatus.PendingActivation"/>
    /// (a fresh OTP is then required) unless <paramref name="adminDirect"/>, in which case it goes
    /// straight to <see cref="AccountStatus.Active"/>. Idempotent — a no-op if already reactivated.
    /// </summary>
    public Result ReactivateAfterDeactivation(DateTime nowUtc, bool adminDirect)
    {
        if (Status is AccountStatus.Active or AccountStatus.PendingActivation)
        {
            return Result.Success();
        }

        if (Status != AccountStatus.Deactivated)
        {
            return Result.Failure(Error.Conflict("E-ACCOUNT-NOT-DEACTIVATED", $"Cannot reactivate an account in status '{Status}'."));
        }

        if (DeactivatedOnUtc is null || nowUtc - DeactivatedOnUtc.Value > DeactivationRetentionWindow)
        {
            return Result.Failure(Error.Conflict("E-ACCOUNT-RETENTION-WINDOW-EXPIRED", "This account's 12-month deactivation retention window has passed."));
        }

        var target = adminDirect ? AccountStatus.Active : AccountStatus.PendingActivation;
        var transition = AccountStateMachine.EnsureTransitionAllowed(Status, target);

        if (transition.IsFailure)
        {
            return transition;
        }

        Status = target;
        DeactivatedOnUtc = null;

        if (target == AccountStatus.Active)
        {
            ActivatedOnUtc = nowUtc;
        }

        UpdatedOnUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>§6.1. Reacts to BC-8's <c>IdentityVerifiedByGovernmentIntegrationEvent</c> (§9.1).</summary>
    public void ApplyGovernmentIdentityVerified(string registry, DateTime nowUtc)
    {
        IdentityVerified = true;
        UpdatedOnUtc = nowUtc;

        Raise(new IdentityVerificationAppliedDomainEvent(Id, registry));
    }
}
