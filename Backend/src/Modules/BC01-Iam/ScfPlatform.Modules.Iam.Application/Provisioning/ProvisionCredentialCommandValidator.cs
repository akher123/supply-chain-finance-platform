using ScfPlatform.Modules.Iam.Application.Common;
using FluentValidation;

namespace ScfPlatform.Modules.Iam.Application.Provisioning;

/// <summary>
/// BC-01-IAM-and-UAM.md §10.3 — basic shape/presence checks. NOTE: the shared
/// <c>ValidationBehavior</c> (CogniJobs.BuildingBlocks.Application) currently collapses every
/// FluentValidation failure into a single generic <c>Validation.Failed</c> code, not the
/// per-field code attached via <c>WithErrorCode</c> below — so this validator alone does not
/// satisfy §12's "preserve Error.Code exactly" requirement. The handler performs the same checks
/// again via the Domain VO factories (which DO return the exact frozen <c>E-REG-*</c> codes) as
/// the authoritative source; this validator exists for fast-fail + good messages, and so a future
/// fix to the shared pipeline picks up the correct codes for free. Flagged in BUILD_REPORT.md.
/// </summary>
public sealed class ProvisionCredentialCommandValidator : AbstractValidator<ProvisionCredentialCommand>
{
    public ProvisionCredentialCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .WithErrorCode(IamErrorCodes.RegInvalidEmail);

        RuleFor(command => command.Mobile)
            .NotEmpty()
            .WithErrorCode(IamErrorCodes.RegInvalidMobile);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(10)
            .WithErrorCode(IamErrorCodes.RegInvalidPassword);

        RuleFor(command => command.Role)
            .NotEmpty()
            .WithErrorCode(IamErrorCodes.RegInvalidRole);
    }
}
