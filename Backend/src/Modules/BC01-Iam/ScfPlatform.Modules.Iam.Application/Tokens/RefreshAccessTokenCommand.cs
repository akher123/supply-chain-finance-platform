using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Services;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>BC-01-IAM-and-UAM.md §10.1 — one-time-use refresh-token rotation.</summary>
public sealed record RefreshAccessTokenCommand(string RefreshToken) : IRequest<Result<RefreshAccessTokenResult>>;

public sealed record RefreshAccessTokenResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresOnUtc);

public sealed class RefreshAccessTokenCommandValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenCommandValidator() => RuleFor(c => c.RefreshToken).NotEmpty();
}

public sealed class RefreshAccessTokenCommandHandler : IRequestHandler<RefreshAccessTokenCommand, Result<RefreshAccessTokenResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IJwtSigner _jwtSigner;
    private readonly IRevokedTokenStore _revokedTokens;
    private readonly TimeProvider _timeProvider;

    public RefreshAccessTokenCommandHandler(
        IUserAccountRepository userAccounts,
        IIamUnitOfWork unitOfWork,
        IJwtSigner jwtSigner,
        IRevokedTokenStore revokedTokens,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _jwtSigner = jwtSigner;
        _revokedTokens = revokedTokens;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RefreshAccessTokenResult>> Handle(RefreshAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var presentedHash = _jwtSigner.HashRefreshToken(request.RefreshToken);

        if (await _revokedTokens.IsRevokedAsync(presentedHash, cancellationToken))
        {
            return Result.Failure<RefreshAccessTokenResult>(Error.Unauthorized(IamErrorCodes.TokenInvalid, "This refresh token has been revoked."));
        }

        var account = await _userAccounts.GetBySessionRefreshTokenHashAsync(presentedHash, cancellationToken);
        var session = account?.Sessions.FirstOrDefault(s => s.RefreshTokenHash == presentedHash);

        if (account is null || session is null || !session.IsActive || session.ExpiresOnUtc <= nowUtc)
        {
            return Result.Failure<RefreshAccessTokenResult>(Error.Unauthorized(IamErrorCodes.TokenInvalid, "This refresh token is invalid, expired, or revoked."));
        }

        var (newRefreshToken, newRefreshTokenHash) = _jwtSigner.IssueRefreshToken();
        var newExpiresOnUtc = nowUtc.Add(session.ExpiresOnUtc - session.IssuedOnUtc);
        var rotateResult = account.RotateSessionRefreshToken(session.Id, newRefreshTokenHash, newExpiresOnUtc, nowUtc);

        if (rotateResult.IsFailure)
        {
            return Result.Failure<RefreshAccessTokenResult>(rotateResult.Error);
        }

        var specResult = TokenClaimsBuilder.BuildAccessToken(
            account.Id.Value, account.Role, account.Permissions, session.Id.Value, [], TimeSpan.FromMinutes(Login.LoginTokenIssuer.AccessTokenTtlMinutes), nowUtc);

        if (specResult.IsFailure)
        {
            return Result.Failure<RefreshAccessTokenResult>(specResult.Error);
        }

        var accessToken = _jwtSigner.SignAccessToken(specResult.Value);

        // The old refresh-token hash must never validate again — the revocation list is the
        // immediate backstop; the session row itself now only recognises the rotated hash.
        await _revokedTokens.AddAsync(presentedHash, nowUtc, session.ExpiresOnUtc, cancellationToken);

        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RefreshAccessTokenResult(accessToken, newRefreshToken, specResult.Value.ExpiresOnUtc));
    }
}
