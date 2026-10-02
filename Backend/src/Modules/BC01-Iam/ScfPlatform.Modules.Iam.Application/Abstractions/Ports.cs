using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;

namespace ScfPlatform.Modules.Iam.Application.Abstractions;

// Ports this module CALLS (BC-01-IAM-and-UAM.md §9.2) — interfaces defined here in Application,
// adapters implemented in Infrastructure. Keep the shapes exactly as documented so production
// adapters drop in later without touching Application.

/// <summary>argon2id hashing — keeps Domain crypto-ignorant.</summary>
public interface IPasswordHasher
{
    PasswordHash Hash(RawPassword raw);

    /// <summary>Constant-time comparison.</summary>
    bool Verify(RawPassword raw, PasswordHash stored);
}

/// <summary>A k-anonymity range query against an external breach corpus.</summary>
public interface IBreachCheckPort
{
    Task<bool> IsBreachedAsync(RawPassword raw, CancellationToken cancellationToken);
}

/// <summary>Access-token signing / refresh-token minting — RS256, keys from config/key vault.</summary>
public interface IJwtSigner
{
    string SignAccessToken(AccessTokenSpec spec);

    (string RefreshToken, string RefreshTokenHash) IssueRefreshToken();

    /// <summary>Signature + expiry only — the revocation-list check is a separate, repository-backed step (§9.3 <c>TokenValidationApi</c>).</summary>
    bool ValidateSignature(string token);

    /// <summary>Deterministically hashes a *presented* refresh token the same way <see cref="IssueRefreshToken"/> hashed it at mint time, so a repository lookup by <c>RefreshTokenHash</c> (§11.1 <c>sessions.refresh_token_hash</c>) can find the owning session. Not part of BC-01's neutral §9.2 port spec — added because the refresh-token flow (§10.1 <c>RefreshAccessTokenCommand</c>) needs it and Application must not do its own crypto.</summary>
    string HashRefreshToken(string refreshToken);

    /// <summary>Reads back the claims <see cref="SignAccessToken"/> put in a token, without re-validating anything — the caller (<c>ValidateTokenQuery</c>) checks expiry/signature/session-state itself. Returns null if the token cannot be parsed. Not part of §9.2's neutral spec — needed so <c>TokenValidationApi</c> (§9.3) can work without Application parsing JWTs itself.</summary>
    JwtClaims? ReadClaims(string token);
}

/// <summary>The claim set <see cref="IJwtSigner.ReadClaims"/> returns — mirrors <see cref="AccessTokenSpec"/>'s shape.</summary>
public sealed record JwtClaims(Guid UserId, string Role, IReadOnlyList<string> Permissions, Guid SessionId, DateTime ExpiresOnUtc);

/// <summary>SMS/email transport for activation, MFA, and reset codes — channel implied by <paramref name="purpose"/> (SMS for mobile-OTP/reset, email/SMS for activation).</summary>
public interface IOtpDeliveryPort
{
    Task<Result> SendAsync(string destination, string plaintextCode, OtpPurpose purpose, CancellationToken cancellationToken);
}

/// <summary>TOTP secret generation/verification for authenticator-app MFA (RFC 6238).</summary>
public interface ITotpProvider
{
    (string SecretRef, string ProvisioningUri) Enroll(string accountLabel);

    bool Verify(string secretRef, string submittedCode);
}

/// <summary>Distributed rate-limiter — registration/login/reset throttling. <paramref name="key"/> examples: <c>"register:{ip}"</c>, <c>"login:{ip}"</c>, <c>"reset:{identifier}"</c>.</summary>
public interface IRateLimiterPort
{
    /// <summary>Returns false when the caller has exceeded the window.</summary>
    Task<bool> TryConsumeAsync(string key, int maxInWindow, TimeSpan window, CancellationToken cancellationToken);
}
