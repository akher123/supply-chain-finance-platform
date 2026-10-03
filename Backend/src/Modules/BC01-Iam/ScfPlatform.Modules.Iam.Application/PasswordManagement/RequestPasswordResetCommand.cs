using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.PasswordManagement;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.5-03 AC-01/09 — always returns success; never reveals whether <paramref name="Identifier"/> exists (no user enumeration, §6.2).</summary>
public sealed record RequestPasswordResetCommand(string Identifier) : IRequest<Result>;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator() => RuleFor(c => c.Identifier).NotEmpty();
}

public sealed class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IRateLimiterPort _rateLimiter;
    private readonly IOtpDeliveryPort _otpDelivery;
    private readonly TimeProvider _timeProvider;

    public RequestPasswordResetCommandHandler(
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

    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (!await _rateLimiter.TryConsumeAsync($"reset:{request.Identifier}", 3, TimeSpan.FromHours(1), cancellationToken))
        {
            return Result.Failure(Error.Failure(IamErrorCodes.ResetRateLimited, "Too many password-reset requests — try again later."));
        }

        var account = await _userAccounts.GetByEmailOrMobileAsync(request.Identifier, cancellationToken);

        if (account is not null)
        {
            var (plaintextCode, codeHash) = OtpCodeGenerator.Generate();
            var challengeResult = OtpChallenge.Issue(account.Id, OtpPurpose.PasswordReset, codeHash, nowUtc, maxAttempts: 3);

            if (challengeResult.IsSuccess)
            {
                await _otpChallenges.AddAsync(challengeResult.Value, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _otpDelivery.SendAsync(account.Credential.Mobile.Value, plaintextCode, OtpPurpose.PasswordReset, cancellationToken);
            }
        }

        // Always success — matching timing/response shape whether or not the identifier exists.
        return Result.Success();
    }
}
