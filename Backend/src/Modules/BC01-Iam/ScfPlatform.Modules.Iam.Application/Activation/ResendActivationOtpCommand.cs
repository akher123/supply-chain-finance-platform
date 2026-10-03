using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Activation;

/// <summary>BC-01-IAM-and-UAM.md §10.1 — re-issues the activation OTP; rate-limited.</summary>
public sealed record ResendActivationOtpCommand(Guid UserId) : IRequest<Result>;

public sealed class ResendActivationOtpCommandValidator : AbstractValidator<ResendActivationOtpCommand>
{
    public ResendActivationOtpCommandValidator() => RuleFor(c => c.UserId).NotEmpty();
}

public sealed class ResendActivationOtpCommandHandler : IRequestHandler<ResendActivationOtpCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IRateLimiterPort _rateLimiter;
    private readonly IOtpDeliveryPort _otpDelivery;
    private readonly TimeProvider _timeProvider;

    public ResendActivationOtpCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        IRateLimiterPort rateLimiter,
        IOtpDeliveryPort otpDelivery,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _rateLimiter = rateLimiter;
        _otpDelivery = otpDelivery;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(ResendActivationOtpCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var accountId = new UserAccountId(request.UserId);

        var account = await _userAccounts.GetByIdAsync(accountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        if (account.Status != AccountStatus.PendingActivation)
        {
            return Result.Failure(Error.Conflict("E-ACCOUNT-ALREADY-ACTIVE", "This account is not pending activation."));
        }

        if (!await _rateLimiter.TryConsumeAsync($"activate-resend:{request.UserId}", 3, TimeSpan.FromMinutes(15), cancellationToken))
        {
            return Result.Failure(Error.Failure(IamErrorCodes.RegRateLimited, "Too many resend attempts — try again later."));
        }

        var priorChallenge = await _otpChallenges.GetActiveByAccountAndPurposeAsync(accountId, OtpPurpose.Activation, cancellationToken);

        if (priorChallenge is not null)
        {
            priorChallenge.MarkExpired();
            _otpChallenges.Update(priorChallenge);
        }

        var (plaintextCode, codeHash) = OtpCodeGenerator.Generate();
        var challengeResult = OtpChallenge.Issue(accountId, OtpPurpose.Activation, codeHash, nowUtc, maxAttempts: 5);

        if (challengeResult.IsFailure)
        {
            return challengeResult;
        }

        await _otpChallenges.AddAsync(challengeResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _otpDelivery.SendAsync(account.Credential.Mobile.Value, plaintextCode, OtpPurpose.Activation, cancellationToken);

        return Result.Success();
    }
}
