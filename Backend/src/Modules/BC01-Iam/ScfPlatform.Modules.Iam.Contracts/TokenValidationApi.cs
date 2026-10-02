using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.Modules.Iam.Contracts;

// Frozen public API port. Source: Handover_Packages/BC-01-IAM-and-UAM.md §9.3.

/// <summary>
/// Used by the host's authentication middleware (and by any module that must check a token
/// out-of-band). Validates signature, expiry, and the revocation list; returns the principal.
/// </summary>
public interface ITokenValidationApi
{
    Task<Result<ValidatedPrincipal>> Validate(string accessToken);
}

public sealed record ValidatedPrincipal(Guid UserId, string Role, IReadOnlyList<string> Permissions, Guid SessionId);
