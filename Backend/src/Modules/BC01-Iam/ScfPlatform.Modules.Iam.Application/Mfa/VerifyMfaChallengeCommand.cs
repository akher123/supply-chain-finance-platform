using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Application.Login;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Mfa;

/// <summary>BC-01-IAM-and-UAM.md §10.1, US-3.1.5-02 — the second-factor step after <c>LoginWithCredentialsCommand</c> returned an MFA challenge.</summary>
public sealed record VerifyMfaChallengeCommand(
    Guid UserId,
    string Code,
    SessionChannel Channel,
    string DeviceFingerprint,
    bool RememberMe,
    bool UseBackupCode) : IRequest<Result<LoginResult>>;

public sealed class VerifyMfaChallengeCommandValidator : AbstractValidator<VerifyMfaChallengeCommand>
{
    public VerifyMfaChallengeCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Code).NotEmpty();
    }
}

public sealed class VerifyMfaChallengeCommandHandler : IRequestHandler<VerifyMfaChallengeCommand, Result<LoginResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IJwtSigner _jwtSigner;
    private readonly ITotpProvider _totpProvider;
    private readonly TimeProvider _timeProvider;

    public VerifyMfaChallengeCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        IJwtSigner jwtSigner,
        ITotpProvider totpProvider,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _jwtSigner = jwtSigner;
        _totpProvider = totpProvider;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResult>> Handle(VerifyMfaChallengeCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var accountId = new UserAccountId(request.UserId);

        var account = await _userAccounts.GetByIdAsync(accountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure<LoginResult>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        if (!account.Mfa.Enabled)
        {
            return Result.Failure<LoginResult>(Error.Conflict(IamErrorCodes.MfaNotEnabled, "MFA is not enabled on this account."));
        }

        var verified = request.UseBackupCode
            ? account.RedeemBackupCode(OtpCodeGenerator.Hash(request.Code), nowUtc).IsSuccess
            : await VerifySecondFactorAsync(account, request.Code, accountId, nowUtc, cancellationToken);

        if (!verified)
        {
            account.RecordOtpFailure(3, nowUtc);
            _userAccounts.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<LoginResult>(account.LockState.IsLocked
                ? Error.Failure(IamErrorCodes.OtpLocked, "Too many incorrect MFA attempts — this account is temporarily locked.")
                : Error.Unauthorized(IamErrorCodes.MfaInvalidCode, "The submitted MFA code is invalid."));
        }

        var issueResult = LoginTokenIssuer.IssueTokens(account, request.Channel, request.DeviceFingerprint, request.RememberMe, nowUtc, _jwtSigner);

        if (issueResult.IsFailure)
        {
            return issueResult;
        }

        _userAccounts.Update(account);
        _unitOfWork.EnqueueIntegrationEvent(new UserLoggedInIntegrationEvent(account.Id.Value, issueResult.Value.SessionId!.Value, request.Channel.ToString(), nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return issueResult;
    }

    private async Task<bool> VerifySecondFactorAsync(Domain.Aggregates.UserAccount account, string code, UserAccountId accountId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (account.Mfa.Method == MfaMethod.Totp)
        {
            return _totpProvider.Verify(account.Mfa.SecretRef!, code);
        }

        var challenge = await _otpChallenges.GetActiveByAccountAndPurposeAsync(accountId, OtpPurpose.Mfa, cancellationToken);

        if (challenge is null)
        {
            return false;
        }

        var result = challenge.Verify(OtpCodeGenerator.Hash(code), nowUtc);
        _otpChallenges.Update(challenge);

        return result.IsSuccess;
    }
}
