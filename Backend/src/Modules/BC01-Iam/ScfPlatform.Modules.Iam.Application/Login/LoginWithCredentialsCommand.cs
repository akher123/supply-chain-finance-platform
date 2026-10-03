using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Login;

/// <summary>BC-01-IAM-and-UAM.md §10.1, US-3.1.5-01. <paramref name="Identifier"/> is auto-detected as email or mobile.</summary>
public sealed record LoginWithCredentialsCommand(
    string Identifier,
    string Password,
    SessionChannel Channel,
    string DeviceFingerprint,
    bool RememberMe,
    string IpAddress) : IRequest<Result<LoginResult>>;

/// <summary>
/// Either an issued-tokens outcome (<see cref="MfaRequired"/> false) or an MFA-challenge handle
/// (<see cref="MfaRequired"/> true, no tokens yet) — BC-01 §12 documents both as a <c>200</c> from
/// the same route.
/// </summary>
public sealed record LoginResult(
    bool MfaRequired,
    Guid? UserId,
    string? AccessToken,
    string? RefreshToken,
    Guid? SessionId,
    DateTime? AccessTokenExpiresOnUtc);

public sealed class LoginWithCredentialsCommandValidator : AbstractValidator<LoginWithCredentialsCommand>
{
    public LoginWithCredentialsCommandValidator()
    {
        RuleFor(c => c.Identifier).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
    }
}
