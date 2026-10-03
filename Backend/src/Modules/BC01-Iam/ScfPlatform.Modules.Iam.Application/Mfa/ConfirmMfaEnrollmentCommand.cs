using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform   .Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Mfa;

/// <summary>BC-01-IAM-and-UAM.md §10.1, US-3.1.5-02 — the verify-before-activate step. On success generates 8–10 backup codes and returns them once, plaintext, for the user to store.</summary>
public sealed record ConfirmMfaEnrollmentCommand(Guid UserId, MfaMethod Method, string SecretRef, string Code) : IRequest<Result<ConfirmMfaEnrollmentResult>>;

public sealed record ConfirmMfaEnrollmentResult(IReadOnlyList<string> BackupCodes);

public sealed class ConfirmMfaEnrollmentCommandValidator : AbstractValidator<ConfirmMfaEnrollmentCommand>
{
    public ConfirmMfaEnrollmentCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.SecretRef).NotEmpty();
        RuleFor(c => c.Code).NotEmpty();
    }
}

public sealed class ConfirmMfaEnrollmentCommandHandler : IRequestHandler<ConfirmMfaEnrollmentCommand, Result<ConfirmMfaEnrollmentResult>>
{
    private const int BackupCodeCount = 10;

    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly ITotpProvider _totpProvider;
    private readonly TimeProvider _timeProvider;

    public ConfirmMfaEnrollmentCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        ITotpProvider totpProvider,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _totpProvider = totpProvider;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ConfirmMfaEnrollmentResult>> Handle(ConfirmMfaEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var accountId = new UserAccountId(request.UserId);
        var account = await _userAccounts.GetByIdAsync(accountId, cancellationToken);

        if (account is null)
        {
            return Result.Failure<ConfirmMfaEnrollmentResult>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        bool verified;

        if (request.Method == MfaMethod.Totp)
        {
            verified = _totpProvider.Verify(request.SecretRef, request.Code);
        }
        else
        {
            var challenge = await _otpChallenges.GetActiveByAccountAndPurposeAsync(accountId, OtpPurpose.Mfa, cancellationToken);
            verified = challenge is not null && challenge.Verify(OtpCodeGenerator.Hash(request.Code), nowUtc).IsSuccess;

            if (challenge is not null)
            {
                _otpChallenges.Update(challenge);
            }
        }

        if (!verified)
        {
            return Result.Failure<ConfirmMfaEnrollmentResult>(Error.Unauthorized(IamErrorCodes.MfaInvalidCode, "The submitted MFA code is invalid."));
        }

        var backupCodes = Enumerable.Range(0, BackupCodeCount)
            .Select(_ => Guid.NewGuid().ToString("N")[..10].ToUpperInvariant())
            .ToList();
        var backupCodeHashes = backupCodes.Select(OtpCodeGenerator.Hash).ToList();

        var enableResult = account.EnableMfa(request.Method, request.SecretRef, backupCodeHashes, nowUtc);

        if (enableResult.IsFailure)
        {
            return Result.Failure<ConfirmMfaEnrollmentResult>(enableResult.Error);
        }

        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConfirmMfaEnrollmentResult(backupCodes));
    }
}
