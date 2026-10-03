using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>
/// BC-01-IAM-and-UAM.md §10.2, §9.3 — the implementation behind <c>TokenValidationApi</c>. Checks
/// signature, expiry, and (in place of a separate access-token revocation list — see
/// <c>IJwtSigner.ReadClaims</c>'s XML doc) that the owning session is still active, which is a
/// stronger and simpler equivalent given sessions are already revoked on logout/suspend/deactivate/
/// password-change (§6.2 invariant #8).
/// </summary>
public sealed record ValidateTokenQuery(string AccessToken) : IRequest<Result<ValidatedPrincipalResult>>;

public sealed record ValidatedPrincipalResult(Guid UserId, string Role, IReadOnlyList<string> Permissions, Guid SessionId);

public sealed class ValidateTokenQueryValidator : AbstractValidator<ValidateTokenQuery>
{
    public ValidateTokenQueryValidator() => RuleFor(q => q.AccessToken).NotEmpty();
}

public sealed class ValidateTokenQueryHandler : IRequestHandler<ValidateTokenQuery, Result<ValidatedPrincipalResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IJwtSigner _jwtSigner;
    private readonly TimeProvider _timeProvider;

    public ValidateTokenQueryHandler(IUserAccountRepository userAccounts, IJwtSigner jwtSigner, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _jwtSigner = jwtSigner;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ValidatedPrincipalResult>> Handle(ValidateTokenQuery request, CancellationToken cancellationToken)
    {
        if (!_jwtSigner.ValidateSignature(request.AccessToken))
        {
            return Result.Failure<ValidatedPrincipalResult>(Error.Unauthorized(IamErrorCodes.TokenInvalid, "Invalid token signature."));
        }

        var claims = _jwtSigner.ReadClaims(request.AccessToken);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (claims is null || claims.ExpiresOnUtc <= nowUtc)
        {
            return Result.Failure<ValidatedPrincipalResult>(Error.Unauthorized(IamErrorCodes.TokenInvalid, "This token is expired or malformed."));
        }

        var account = await _userAccounts.GetByIdAsync(new UserAccountId(claims.UserId), cancellationToken);
        var session = account?.Sessions.FirstOrDefault(s => s.Id == new SessionId(claims.SessionId));

        if (account is null || session is null || !session.IsActive)
        {
            return Result.Failure<ValidatedPrincipalResult>(Error.Unauthorized(IamErrorCodes.TokenInvalid, "This token's session has been revoked."));
        }

        return Result.Success(new ValidatedPrincipalResult(claims.UserId, claims.Role, claims.Permissions, claims.SessionId));
    }
}
