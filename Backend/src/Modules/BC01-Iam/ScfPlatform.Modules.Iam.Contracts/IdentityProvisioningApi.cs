using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.Modules.Iam.Contracts;

// Frozen public API port. Source: Handover_Packages/BC-01-IAM-and-UAM.md §9.3.
// "Contract stability": BC-2 and BC-3 reference ProvisionCredential by signature — keep
// the operation name, parameter order, return type, and E-REG-* error codes exact.

/// <summary>
/// Called synchronously by BC-2 Employer Profile and BC-3 JobSeeker Profile during their
/// registration journeys. Enforces email/mobile uniqueness, password policy, breach check,
/// argon2id hashing; creates the account in PendingActivation; issues + sends an activation
/// OTP; publishes <see cref="UserRegisteredIntegrationEvent"/>.
/// </summary>
public interface IIdentityProvisioningApi
{
    /// <summary>Returns the new <see cref="ProvisionedIdentity"/> so the caller can build its own aggregate in the same logical unit of work.</summary>
    Task<Result<ProvisionedIdentity>> ProvisionCredential(string email, string mobile, string password, string role);
}

public sealed record ProvisionedIdentity(Guid UserId);

/// <summary>Error.Code values <see cref="IIdentityProvisioningApi.ProvisionCredential"/> returns — callers surface these verbatim.</summary>
public static class IdentityProvisioningErrorCodes
{
    public const string DuplicateEmailOrMobile = "E-REG-DUPLICATE";
    public const string InvalidEmail = "E-REG-INVALID-EMAIL";
    public const string InvalidMobile = "E-REG-INVALID-MOBILE";
    public const string InvalidPassword = "E-REG-INVALID-PASSWORD";
    public const string PasswordBreached = "E-REG-PASSWORD-BREACHED";
    public const string RateLimited = "E-REG-RATE-LIMITED";
}
