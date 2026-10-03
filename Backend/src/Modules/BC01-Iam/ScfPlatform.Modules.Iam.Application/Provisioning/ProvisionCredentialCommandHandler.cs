using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Services;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using ScfPlatform.Modules.Iam.Contracts;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Provisioning;

/// <summary>BC-01-IAM-and-UAM.md §14.1 worked vertical slice — pattern-match every other handler against this one.</summary>
public sealed class ProvisionCredentialCommandHandler : IRequestHandler<ProvisionCredentialCommand, Result<ProvisionCredentialResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBreachCheckPort _breachCheck;
    private readonly IRateLimiterPort _rateLimiter;
    private readonly IOtpDeliveryPort _otpDelivery;
    private readonly TimeProvider _timeProvider;

    public ProvisionCredentialCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IBreachCheckPort breachCheck,
        IRateLimiterPort rateLimiter,
        IOtpDeliveryPort otpDelivery,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _breachCheck = breachCheck;
        _rateLimiter = rateLimiter;
        _otpDelivery = otpDelivery;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ProvisionCredentialResult>> Handle(ProvisionCredentialCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // a. Rate limit. The frozen IdentityProvisioningApi has no caller-origin/IP parameter
        // (it's an in-process call from BC-2/BC-3, not this module's own HTTP boundary) — see
        // BUILD_REPORT.md for the write-up. Rate-limit by identifier as the closest available key.
        if (!await _rateLimiter.TryConsumeAsync($"register:{request.Email}", 5, TimeSpan.FromHours(1), cancellationToken))
        {
            return Result.Failure<ProvisionCredentialResult>(Error.Failure(IamErrorCodes.RegRateLimited, "Too many registration attempts — try again later."));
        }

        // Field-shape validation via the Domain VO factories — see
        // ProvisionCredentialCommandValidator's XML doc for why this is the authoritative source
        // of the exact E-REG-* Error.Code, not the shared FluentValidation pipeline.
        var emailResult = EmailAddress.Create(request.Email);

        if (emailResult.IsFailure)
        {
            return Result.Failure<ProvisionCredentialResult>(emailResult.Error);
        }

        var mobileResult = MobileNumber.Create(request.Mobile);

        if (mobileResult.IsFailure)
        {
            return Result.Failure<ProvisionCredentialResult>(mobileResult.Error);
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return Result.Failure<ProvisionCredentialResult>(Error.Validation(IamErrorCodes.RegInvalidRole, $"'{request.Role}' is not a recognised role."));
        }

        // b. Uniqueness pre-check (the DB unique index is the backstop, §6.2 invariant #2).
        if (await _userAccounts.EmailExistsAsync(emailResult.Value.Value, cancellationToken)
            || await _userAccounts.MobileExistsAsync(mobileResult.Value.Value, cancellationToken))
        {
            return Result.Failure<ProvisionCredentialResult>(Error.Conflict(IamErrorCodes.RegDuplicate, "This email or mobile number is already registered."));
        }

        var rawPasswordResult = RawPassword.Create(request.Password);

        if (rawPasswordResult.IsFailure)
        {
            return Result.Failure<ProvisionCredentialResult>(rawPasswordResult.Error);
        }

        // c. Password policy.
        var policyResult = PasswordPolicyService.Validate(rawPasswordResult.Value, PasswordPolicyContext.Registration);

        if (policyResult.IsFailure)
        {
            return Result.Failure<ProvisionCredentialResult>(policyResult.Error);
        }

        // d. Breach check.
        if (await _breachCheck.IsBreachedAsync(rawPasswordResult.Value, cancellationToken))
        {
            return Result.Failure<ProvisionCredentialResult>(Error.Validation(IamErrorCodes.RegPasswordBreached, "This password has appeared in a known data breach."));
        }

        // e. Hash — the plaintext is discarded after this point.
        var passwordHash = _passwordHasher.Hash(rawPasswordResult.Value);

        // f. Create the account.
        var provisionResult = UserAccount.Provision(emailResult.Value, mobileResult.Value, passwordHash, role, nowUtc);

        if (provisionResult.IsFailure)
        {
            return Result.Failure<ProvisionCredentialResult>(provisionResult.Error);
        }

        var account = provisionResult.Value;

        // g. Issue the activation OTP.
        var (plaintextCode, codeHash) = OtpCodeGenerator.Generate();
        var challengeResult = OtpChallenge.Issue(account.Id, OtpPurpose.Activation, codeHash, nowUtc, maxAttempts: 5);

        if (challengeResult.IsFailure)
        {
            return Result.Failure<ProvisionCredentialResult>(challengeResult.Error);
        }

        // h. Persist account + challenge + the UserRegistered outbox row in one unit of work.
        await _userAccounts.AddAsync(account, cancellationToken);
        await _otpChallenges.AddAsync(challengeResult.Value, cancellationToken);

        _unitOfWork.EnqueueIntegrationEvent(new UserRegisteredIntegrationEvent(
            account.Id.Value,
            account.Role.ToString(),
            account.Credential.Email.Value,
            nowUtc));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Deliver the OTP after the unit of work committed — the plaintext never touches the
        // outbox/database (only the hash does); see OtpIssuedDomainEvent's XML doc for why this
        // isn't wired through the post-commit domain-event dispatch pipeline instead.
        await _otpDelivery.SendAsync(account.Credential.Mobile.Value, plaintextCode, OtpPurpose.Activation, cancellationToken);

        return Result.Success(new ProvisionCredentialResult(account.Id.Value));
    }
}
