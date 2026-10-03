using FluentValidation;
using MediatR;
using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Application.Mfa;

/// <summary>
/// BC-01-IAM-and-UAM.md §10.1, US-3.1.5-02 — begins MFA setup. Does not enable MFA yet; the
/// caller must complete <see cref="ConfirmMfaEnrollmentCommand"/> with the returned
/// <see cref="EnrollMfaResult.SecretRef"/> and a valid code first (verify-before-activate).
/// </summary>
public sealed record EnrollMfaCommand(Guid UserId, MfaMethod Method) : IRequest<Result<EnrollMfaResult>>;

public sealed record EnrollMfaResult(string SecretRef, string? ProvisioningUri);

public sealed class EnrollMfaCommandValidator : AbstractValidator<EnrollMfaCommand>
{
    public EnrollMfaCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Method).Must(m => m is MfaMethod.Totp or MfaMethod.SmsOtp);
    }
}

public sealed class EnrollMfaCommandHandler : IRequestHandler<EnrollMfaCommand, Result<EnrollMfaResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly ITotpProvider _totpProvider;
    private readonly IOtpDeliveryPort _otpDelivery;
    private readonly TimeProvider _timeProvider;

    public EnrollMfaCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        ITotpProvider totpProvider,
        IOtpDeliveryPort otpDelivery,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _totpProvider = totpProvider;
        _otpDelivery = otpDelivery;
        _timeProvider = timeProvider;
    }

    public async Task<Result<EnrollMfaResult>> Handle(EnrollMfaCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var accountId = new UserAccountId(request.UserId);
        var account = await _userAccounts.GetByIdAsync(accountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure<EnrollMfaResult>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        if (account.Status != AccountStatus.Active)
        {
            return Result.Failure<EnrollMfaResult>(Error.Conflict("E-ACCOUNT-NOT-ACTIVE", "MFA can only be enrolled on an active account."));
        }

        if (account.Mfa.Enabled)
        {
            return Result.Failure<EnrollMfaResult>(Error.Conflict(IamErrorCodes.MfaAlreadyEnabled, "MFA is already enabled on this account."));
        }

        if (request.Method == MfaMethod.Totp)
        {
            var (secretRef, provisioningUri) = _totpProvider.Enroll(account.Credential.Email.Value);

            return Result.Success(new EnrollMfaResult(secretRef, provisioningUri));
        }

        // SmsOtp — send a test OTP now so the caller can confirm immediately. secretRef here is
        // just an opaque per-enrolment correlation id (SmsOtp has no cryptographic secret) —
        // still populated so the shared MfaConfiguration VO's `Enabled ⇒ SecretRef != null`
        // invariant holds uniformly across both methods.
        var smsSecretRef = Guid.NewGuid().ToString("N");
        var (plaintextCode, codeHash) = OtpCodeGenerator.Generate();
        var challengeResult = OtpChallenge.Issue(accountId, OtpPurpose.Mfa, codeHash, nowUtc, maxAttempts: 3);

        if (challengeResult.IsFailure)
        {
            return Result.Failure<EnrollMfaResult>(challengeResult.Error);
        }

        await _otpChallenges.AddAsync(challengeResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _otpDelivery.SendAsync(account.Credential.Mobile.Value, plaintextCode, OtpPurpose.Mfa, cancellationToken);

        return Result.Success(new EnrollMfaResult(smsSecretRef, null));
    }
}
