using ScfPlatform.BuildingBlocks.Domain;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Provisioning;

/// <summary>
/// BC-01-IAM-and-UAM.md §10.1, §14.1. The implementation behind <c>IdentityProvisioningApi</c> —
/// called synchronously by BC-2 Employer Profile and BC-3 JobSeeker Profile during registration.
/// </summary>
public sealed record ProvisionCredentialCommand(string Email, string Mobile, string Password, string Role)
    : IRequest<Result<ProvisionCredentialResult>>;

public sealed record ProvisionCredentialResult(Guid UserId);
