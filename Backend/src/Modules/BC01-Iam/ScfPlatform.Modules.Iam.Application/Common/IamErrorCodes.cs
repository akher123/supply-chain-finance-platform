namespace ScfPlatform.Modules.Iam.Application.Common;

/// <summary>
/// The <c>Error.Code</c> strings BC-01-IAM-and-UAM.md §12 documents — centralised here so every
/// handler uses the exact same literal instead of re-typing it. The `E-REG-*` subset also exists
/// as literals in the Domain VOs (which cannot reference this Application-layer type — see the
/// 5-layer dependency rule) and in this module's frozen `Contracts.IdentityProvisioningErrorCodes`;
/// all three must stay in sync by value.
/// </summary>
public static class IamErrorCodes
{
    // §9.3 IdentityProvisioningApi (mirrors Contracts.IdentityProvisioningErrorCodes exactly).
    public const string RegDuplicate = "E-REG-DUPLICATE";
    public const string RegInvalidEmail = "E-REG-INVALID-EMAIL";
    public const string RegInvalidMobile = "E-REG-INVALID-MOBILE";
    public const string RegInvalidPassword = "E-REG-INVALID-PASSWORD";
    public const string RegPasswordBreached = "E-REG-PASSWORD-BREACHED";
    public const string RegRateLimited = "E-REG-RATE-LIMITED";
    public const string RegInvalidRole = "E-REG-INVALID-ROLE"; // defensive only — not in the frozen Contracts list; the two legitimate callers (BC-2/BC-3) never send an invalid role.

    // Login (§12 `/api/identity/login`).
    public const string LoginInvalidCredentials = "E-LOGIN-INVALID-CREDENTIALS";
    public const string LoginAccountNotActivated = "E-LOGIN-ACCOUNT-NOT-ACTIVATED";
    public const string LoginAccountDeactivated = "E-LOGIN-ACCOUNT-DEACTIVATED";
    public const string LoginAccountBanned = "E-LOGIN-ACCOUNT-BANNED";
    public const string LoginAccountLocked = "E-LOGIN-ACCOUNT-LOCKED";
    public const string LoginRateLimited = "E-LOGIN-RATE-LIMITED";

    // MFA.
    public const string MfaInvalidCode = "E-MFA-INVALID-CODE";
    public const string MfaAlreadyEnabled = "E-MFA-ALREADY-ENABLED";
    public const string MfaNotEnabled = "E-MFA-NOT-ENABLED";
    public const string MfaReauthRequired = "E-MFA-REAUTH-REQUIRED";

    // OTP (activation / mfa / password-reset challenges).
    public const string OtpInvalid = "E-OTP-INVALID";
    public const string OtpExpired = "E-OTP-EXPIRED";
    public const string OtpLocked = "E-OTP-LOCKED";

    // Tokens.
    public const string TokenInvalid = "E-TOKEN-INVALID";

    // OAuth (§12 `/api/identity/oauth/token`).
    public const string OAuthInvalidGrant = "E-OAUTH-INVALID-GRANT";
    public const string OAuthInvalidClient = "E-OAUTH-INVALID-CLIENT";

    // Password reset (§9.3-adjacent — the reset-path mirror of the E-REG-* codes).
    public const string ResetTokenInvalid = "E-RESET-TOKEN-INVALID";
    public const string ResetTokenExpired = "E-RESET-TOKEN-EXPIRED";
    public const string ResetInvalidPassword = "E-RESET-INVALID-PASSWORD";
    public const string ResetPasswordBreached = "E-RESET-PASSWORD-BREACHED";
    public const string ResetRateLimited = "E-RESET-RATE-LIMITED";
    public const string ResetPasswordReused = "E-RESET-PASSWORD-REUSED";

    // RBAC (§6.2 invariant, §12 admin routes).
    public const string UnauthorizedRole = "E-UNAUTHORIZED-ROLE";
    public const string Forbidden = "E-FORBIDDEN";

    // Generic not-found codes — §12 documents these routes as plain "404" with no bespoke E-code;
    // these give the API layer something specific to log/branch on internally while still mapping to 404.
    public const string AccountNotFound = "E-ACCOUNT-NOT-FOUND";
    public const string OtpChallengeNotFound = "E-OTP-NOT-FOUND";
}
