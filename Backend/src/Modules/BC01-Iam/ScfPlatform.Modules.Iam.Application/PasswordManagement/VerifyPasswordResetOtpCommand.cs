using System.Security.Cryptography;
using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.PasswordManagement;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.5-03 AC-02/03/04.</summary>
public sealed record VerifyPasswordResetOtpCommand(string Identifier, string Otp) : IRequest<Result<VerifyPasswordResetOtpResult>>;

public sealed record VerifyPasswordResetOtpResult(string ResetToken);

public sealed class VerifyPasswordResetOtpCommandValidator : AbstractValidator<VerifyPasswordResetOtpCommand>
{
    public VerifyPasswordResetOtpCommandValidator()
    {
        RuleFor(c => c.Identifier).NotEmpty();
        RuleFor(c => c.Otp).Matches(@"^\d{6}$");
    }
}

public sealed class VerifyPasswordResetOtpCommandHandler : IRequestHandler<VerifyPasswordResetOtpCommand, Result<VerifyPasswordResetOtpResult>>
{
    private static readonly TimeSpan ResetTokenTtl = TimeSpan.FromMinutes(15);

    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public VerifyPasswordResetOtpCommandHandler(
        IUserAccountRepository userAccounts, IOtpChallengeRepository otpChallenges, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<VerifyPasswordResetOtpResult>> Handle(VerifyPasswordResetOtpCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var account = await _userAccounts.GetByEmailOrMobileAsync(request.Identifier, cancellationToken);

        // No enumeration: an unknown identifier fails exactly like a wrong OTP would.
        if (account is null)
        {
            return Result.Failure<VerifyPasswordResetOtpResult>(Error.Validation(IamErrorCodes.OtpInvalid, "The submitted code does not match."));
        }

        var challenge = await _otpChallenges.GetActiveByAccountAndPurposeAsync(account.Id, OtpPurpose.PasswordReset, cancellationToken);

        if (challenge is null)
        {
            return Result.Failure<VerifyPasswordResetOtpResult>(Error.Validation(IamErrorCodes.OtpInvalid, "The submitted code does not match."));
        }

        var verifyResult = challenge.Verify(OtpCodeGenerator.Hash(request.Otp), nowUtc);
        _otpChallenges.Update(challenge);

        if (verifyResult.IsFailure)
        {
            account.RecordOtpFailure(challenge.MaxAttempts, nowUtc);
            _userAccounts.Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<VerifyPasswordResetOtpResult>(verifyResult.Error);
        }

        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        account.IssuePasswordResetToken(OtpCodeGenerator.Hash(resetToken), nowUtc, nowUtc.Add(ResetTokenTtl));

        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new VerifyPasswordResetOtpResult(resetToken));
    }
}
