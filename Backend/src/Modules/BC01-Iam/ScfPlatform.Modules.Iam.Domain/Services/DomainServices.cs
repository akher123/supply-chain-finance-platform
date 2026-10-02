using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;

namespace ScfPlatform.Modules.Iam.Domain.Services;

// Domain services (BC-01-IAM-and-UAM.md §7) — stateless; pure logic that spans entities or needs
// a small external input, kept in Domain (crypto/signing/IO are ports in Application/Infrastructure).

/// <summary>Which caller path is validating a password — selects the contract error code (§7.1: "E-REG-INVALID-PASSWORD (or E-RESET-INVALID-PASSWORD on the reset path)").</summary>
public enum PasswordPolicyContext
{
    Registration,
    Reset,
}

/// <summary>§7.1 — pure. Enforces the password policy: min length 10, at least 3 of {lowercase, uppercase, digit, symbol}, not a trivial sequence.</summary>
public static class PasswordPolicyService
{
    private const int MinimumLength = 10;
    private const int MinimumCharacterClasses = 3;

    private static readonly string[] TrivialSequences =
    [
        "0123456789", "1234567890", "9876543210", "qwertyuiop", "asdfghjkl", "zxcvbnm",
    ];

    public static Result Validate(RawPassword candidate, PasswordPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var errorCode = context == PasswordPolicyContext.Registration
            ? "E-REG-INVALID-PASSWORD"
            : "E-RESET-INVALID-PASSWORD";

        var value = candidate.Value;

        if (value.Length < MinimumLength)
        {
            return Result.Failure(Error.Validation(errorCode, $"Password must be at least {MinimumLength} characters long."));
        }

        if (CountCharacterClasses(value) < MinimumCharacterClasses)
        {
            return Result.Failure(Error.Validation(
                errorCode,
                "Password must mix at least 3 of: lowercase letters, uppercase letters, digits, symbols."));
        }

        if (IsTrivialSequence(value))
        {
            return Result.Failure(Error.Validation(errorCode, "Password must not be a trivial, predictable sequence."));
        }

        return Result.Success();
    }

    private static int CountCharacterClasses(string value)
    {
        var hasLower = value.Any(char.IsLower);
        var hasUpper = value.Any(char.IsUpper);
        var hasDigit = value.Any(char.IsDigit);
        var hasSymbol = value.Any(c => !char.IsLetterOrDigit(c));

        return (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
    }

    private static bool IsTrivialSequence(string value)
    {
        var lower = value.ToLowerInvariant();

        if (lower.Distinct().Count() == 1)
        {
            return true;
        }

        return TrivialSequences.Any(sequence => lower.Contains(sequence, StringComparison.Ordinal));
    }
}

/// <summary>§7.2 — pure. Maps a role to its baseline permission set and unions explicit admin grants.</summary>
public static class PermissionResolver
{
    private static readonly IReadOnlyDictionary<UserRole, IReadOnlyList<string>> BaselinePermissions =
        new Dictionary<UserRole, IReadOnlyList<string>>
        {
            [UserRole.JobSeeker] = ["profile:self", "applications:self", "search:read"],
            [UserRole.Employer] = ["employer:self", "jobs:write", "applications:read", "candidates:read"],
            [UserRole.ThirdPartyPortal] = ["integrations:read", "jobs:write"],
            [UserRole.MoLAdministrator] =
            [
                "users:manage", "jobs:moderate", "taxonomy:manage", "reports:read",
                // MoLAdministrator additionally carries everything the other roles carry (§7.2).
                "profile:self", "applications:self", "search:read",
                "employer:self", "jobs:write", "applications:read", "candidates:read",
                "integrations:read",
            ],
        };

    public static IReadOnlyList<string> Resolve(UserRole role, IReadOnlyList<string>? explicitGrants = null)
    {
        var baseline = BaselinePermissions.TryGetValue(role, out var permissions)
            ? permissions
            : Array.Empty<string>();

        if (explicitGrants is null || explicitGrants.Count == 0)
        {
            return baseline;
        }

        return baseline.Union(explicitGrants, StringComparer.Ordinal).ToList();
    }
}

/// <summary>§7.3 — pure. Assembles the access-token claim spec; the <c>JwtSigner</c> port (§9.2) does the actual signing.</summary>
public static class TokenClaimsBuilder
{
    public static readonly TimeSpan MaximumAccessTokenTtl = TimeSpan.FromHours(1);

    public static Result<AccessTokenSpec> BuildAccessToken(
        Guid userId,
        UserRole role,
        IReadOnlyList<string> permissions,
        Guid sessionId,
        IReadOnlyList<string> scopes,
        TimeSpan ttl,
        DateTime nowUtc)
    {
        if (ttl <= TimeSpan.Zero || ttl > MaximumAccessTokenTtl)
        {
            return Result.Failure<AccessTokenSpec>(
                Error.Validation("E-TOKEN-TTL-INVALID", $"Access-token TTL must be > 0 and <= {MaximumAccessTokenTtl}."));
        }

        return AccessTokenSpec.Create(userId, role, permissions, scopes, sessionId, nowUtc.Add(ttl));
    }
}

/// <summary>§7.4 — pure. Centralises §6.2 invariant #1's legal-transition table so it lives in exactly one place.</summary>
public static class AccountStateMachine
{
    private static readonly HashSet<(AccountStatus From, AccountStatus To)> LegalTransitions =
    [
        (AccountStatus.PendingActivation, AccountStatus.Active),
        (AccountStatus.PendingActivation, AccountStatus.Suspended), // employer-reject path only
        (AccountStatus.Active, AccountStatus.Suspended),
        (AccountStatus.Suspended, AccountStatus.Active), // Reinstate
        (AccountStatus.Active, AccountStatus.Deactivated),
        (AccountStatus.Deactivated, AccountStatus.PendingActivation), // ReactivateAfterDeactivation
        (AccountStatus.Deactivated, AccountStatus.Active), // admin reactivates directly
    ];

    /// <summary>
    /// Strict lookup against the legal-transition table — same-state "transitions" are NOT
    /// auto-allowed here. Aggregate methods that are documented as idempotent (e.g. <c>Activate</c>
    /// when already <c>Active</c>) short-circuit before calling this, rather than this service
    /// special-casing same-state pairs.
    /// </summary>
    public static Result EnsureTransitionAllowed(AccountStatus from, AccountStatus to)
    {
        if (LegalTransitions.Contains((from, to)))
        {
            return Result.Success();
        }

        return Result.Failure(Error.Conflict(
            "E-ACCOUNT-STATUS-TRANSITION-INVALID",
            $"Cannot transition account status from '{from}' to '{to}'."));
    }
}
