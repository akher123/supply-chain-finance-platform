using MediatR;
using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Contracts;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;

namespace ScfPlatform.Modules.Iam.Application.Login;

/// <summary>BC-01-IAM-and-UAM.md §10.1, §14.2 US-3.1.5-01.</summary>
public sealed class LoginWithCredentialsCommandHandler : IRequestHandler<LoginWithCredentialsCommand, Result<LoginResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtSigner _jwtSigner;
    private readonly IRateLimiterPort _rateLimiter;
    private readonly IOtpDeliveryPort _otpDelivery;
    private readonly TimeProvider _timeProvider;

    public LoginWithCredentialsCommandHandler(
        IUserAccountRepository userAccounts,
        IOtpChallengeRepository otpChallenges,
        IIamUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtSigner jwtSigner,
        IRateLimiterPort rateLimiter,
        IOtpDeliveryPort otpDelivery,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _otpChallenges = otpChallenges;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtSigner = jwtSigner;
        _rateLimiter = rateLimiter;
        _otpDelivery = otpDelivery;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoginResult>> Handle(LoginWithCredentialsCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // TEMPORARILY DISABLED (2026-07-07, at user's request) — the real 10/min-per-IP login rate
        // limit was tripping mid-run during the frontendlivetest E2E suite, since every test logs in
        // fresh from the same localhost IP and the suite's own login volume exceeds 10/min on average.
        // Re-enable this block before shipping/production use.
        // if (!await _rateLimiter.TryConsumeAsync($"login:{request.IpAddress}", 10, TimeSpan.FromMinutes(1), cancellationToken))
        // {
        //     return Result.Failure<LoginResult>(Error.Failure(IamErrorCodes.LoginRateLimited, "Too many login attempts from this address — try again later."));
        // }

        var account = await _userAccounts.GetByEmailOrMobileAsync(request.Identifier, cancellationToken);
        var rawPasswordResult = RawPassword.Create(request.Password);

        var credentialsValid = account is not null
            && rawPasswordResult.IsSuccess
            && _passwordHasher.Verify(rawPasswordResult.Value, account.Credential.PasswordHash);

        if (!credentialsValid)
        {
            if (account is not null)
            {
                account.RecordFailedLogin(nowUtc);
                _userAccounts.Update(account);
            }

            // Never reveal whether the identifier exists — same generic code either way (§6.2 "no user enumeration").
            _unitOfWork.EnqueueIntegrationEvent(new UserLoginFailedIntegrationEvent(request.Identifier, "invalid-credentials", nowUtc));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<LoginResult>(Error.Unauthorized(IamErrorCodes.LoginInvalidCredentials, "Invalid credentials."));
        }

        var statusError = account!.Status switch
        {
            AccountStatus.PendingActivation => Error.Forbidden(IamErrorCodes.LoginAccountNotActivated, "This account has not been activated yet."),
            AccountStatus.Deactivated => Error.Forbidden(IamErrorCodes.LoginAccountDeactivated, "This account has been deactivated."),
            AccountStatus.Suspended => Error.Forbidden(IamErrorCodes.LoginAccountBanned, "This account has been suspended."),
            _ => Error.None,
        };

        if (statusError != Error.None)
        {
            _unitOfWork.EnqueueIntegrationEvent(new UserLoginFailedIntegrationEvent(request.Identifier, statusError.Code, nowUtc));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<LoginResult>(statusError);
        }

        if (account.LockState.IsLocked && account.LockState.LockedUntilUtc > nowUtc)
        {
            _unitOfWork.EnqueueIntegrationEvent(new UserLoginFailedIntegrationEvent(request.Identifier, "account-locked", nowUtc));
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<LoginResult>(Error.Failure(IamErrorCodes.LoginAccountLocked, "This account is temporarily locked."));
        }

        if (account.Mfa.Enabled)
        {
            if (account.Mfa.Method == MfaMethod.SmsOtp)
            {
                var (plaintextCode, codeHash) = OtpCodeGenerator.Generate();
                var challengeResult = ScfPlatform.Modules.Iam.Domain.Aggregates.OtpChallenge.Issue(account.Id, OtpPurpose.Mfa, codeHash, nowUtc, maxAttempts: 3);

                if (challengeResult.IsFailure)
                {
                    return Result.Failure<LoginResult>(challengeResult.Error);
                }

                await _otpChallenges.AddAsync(challengeResult.Value, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _otpDelivery.SendAsync(account.Credential.Mobile.Value, plaintextCode, OtpPurpose.Mfa, cancellationToken);
            }

            return Result.Success(new LoginResult(true, account.Id.Value, null, null, null, null));
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
}
