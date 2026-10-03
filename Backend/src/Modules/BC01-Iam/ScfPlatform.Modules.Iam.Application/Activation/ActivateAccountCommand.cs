using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Activation;

/// <summary>BC-01-IAM-and-UAM.md §10.1 — `PendingActivation → Active` via a submitted OTP.</summary>
public sealed record ActivateAccountCommand(Guid UserId, string Otp) : IRequest<Result>;

public sealed class ActivateAccountCommandValidator : AbstractValidator<ActivateAccountCommand>
{
    public ActivateAccountCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Otp).Matches(@"^\d{6}$");
    }
}

public sealed class ActivateAccountCommandHandler : IRequestHandler<ActivateAccountCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ActivateAccountCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(ActivateAccountCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var accountId = new UserAccountId(request.UserId);

        var account = await _userAccounts.GetByIdAsync(accountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var challenge = await _otpChallenges.GetActiveByAccountAndPurposeAsync(accountId, OtpPurpose.Activation, cancellationToken);

        if (challenge is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.OtpChallengeNotFound, "No pending activation OTP for this account."));
        }

        var verifyResult = challenge.Verify(OtpCodeGenerator.Hash(request.Otp), nowUtc);
        _otpChallenges.Update(challenge);

        if (verifyResult.IsFailure)
        {
            account.RecordOtpFailure(challenge.MaxAttempts, nowUtc);
            _userAccounts.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return verifyResult;
        }

        var activateResult = account.Activate(nowUtc);

        if (activateResult.IsFailure)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return activateResult;
        }

        _userAccounts.Update(account);
        _unitOfWork.EnqueueIntegrationEvent(new UserAccountActivatedIntegrationEvent(account.Id.Value, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
