using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Domain.Services;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>
/// BC-01-IAM-and-UAM.md §10.1, US-3.4.3-04 — issues an OAuth2 token-response-shaped access +
/// refresh token for the <c>client_credentials</c> and <c>authorization_code</c> grants.
/// <para>
/// Design note (flagged in BUILD_REPORT.md): §1 explicitly puts OAuth client
/// registration/consent UI out of scope for this module ("a future BC-11/BC-12 concern"), and
/// §11.1's schema has no client-registry or authorization-code-store tables. Rather than invent
/// that missing infrastructure, both grants reuse the existing <c>UserAccount</c>/credential
/// model: a <c>client_credentials</c> caller authenticates exactly like a
/// <see cref="UserRole.ThirdPartyPortal"/> account logging in (client id = email, client secret =
/// password), and <c>authorization_code</c> takes the already-authenticated resource owner's
/// <see cref="UserAccountId"/> directly (the out-of-scope authorize/consent step is assumed to
/// have happened out-of-band) rather than a real opaque code + PKCE code-store.
/// </para>
/// </summary>
public sealed record IssueOAuthTokenCommand(
    string GrantType,
    string ClientId,
    string? ClientSecret,
    Guid? ResourceOwnerUserId,
    IReadOnlyList<string> Scopes) : IRequest<Result<OAuthTokenResult>>;

public sealed record OAuthTokenResult(string AccessToken, string RefreshToken, string TokenType, int ExpiresInSeconds, IReadOnlyList<string> Scopes);

public sealed class IssueOAuthTokenCommandValidator : AbstractValidator<IssueOAuthTokenCommand>
{
    public IssueOAuthTokenCommandValidator()
    {
        RuleFor(c => c.GrantType).Must(g => g is "client_credentials" or "authorization_code");
        RuleFor(c => c.ClientId).NotEmpty();
    }
}

public sealed class IssueOAuthTokenCommandHandler : IRequestHandler<IssueOAuthTokenCommand, Result<OAuthTokenResult>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtSigner _jwtSigner;
    private readonly TimeProvider _timeProvider;

    public IssueOAuthTokenCommandHandler(
        IUserAccountRepository userAccounts,
        IIamUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtSigner jwtSigner,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtSigner = jwtSigner;
        _timeProvider = timeProvider;
    }

    public async Task<Result<OAuthTokenResult>> Handle(IssueOAuthTokenCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        UserAccount? account;

        if (request.GrantType == "client_credentials")
        {
            account = await _userAccounts.GetByEmailAsync(request.ClientId, cancellationToken);
            var rawSecret = RawPassword.Create(request.ClientSecret);

            if (account is null
                || account.Role != UserRole.ThirdPartyPortal
                || account.Status != AccountStatus.Active
                || rawSecret.IsFailure
                || !_passwordHasher.Verify(rawSecret.Value, account.Credential.PasswordHash))
            {
                return Result.Failure<OAuthTokenResult>(Error.Unauthorized(IamErrorCodes.OAuthInvalidClient, "Invalid client credentials."));
            }
        }
        else
        {
            if (request.ResourceOwnerUserId is null)
            {
                return Result.Failure<OAuthTokenResult>(Error.Validation(IamErrorCodes.OAuthInvalidGrant, "A resource-owner UserId is required for the authorization_code grant."));
            }

            account = await _userAccounts.GetByIdAsync(new UserAccountId(request.ResourceOwnerUserId.Value), cancellationToken);

            if (account is null || account.Status != AccountStatus.Active)
            {
                return Result.Failure<OAuthTokenResult>(Error.Validation(IamErrorCodes.OAuthInvalidGrant, "Invalid or inactive resource owner."));
            }
        }

        var (refreshToken, refreshTokenHash) = _jwtSigner.IssueRefreshToken();
        var deviceFingerprint = DeviceFingerprint.Create($"oauth-client:{request.ClientId}").Value;
        var loginResult = account.RecordSuccessfulLogin(SessionChannel.Api, deviceFingerprint, refreshTokenHash, nowUtc, rememberMe: false);

        if (loginResult.IsFailure)
        {
            return Result.Failure<OAuthTokenResult>(loginResult.Error);
        }

        var session = account.Sessions[^1];
        var specResult = TokenClaimsBuilder.BuildAccessToken(
            account.Id.Value, account.Role, account.Permissions, session.Id.Value, request.Scopes, TimeSpan.FromMinutes(Login.LoginTokenIssuer.AccessTokenTtlMinutes), nowUtc);

        if (specResult.IsFailure)
        {
            return Result.Failure<OAuthTokenResult>(specResult.Error);
        }

        var accessToken = _jwtSigner.SignAccessToken(specResult.Value);

        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new OAuthTokenResult(
            accessToken, refreshToken, "Bearer", Login.LoginTokenIssuer.AccessTokenTtlMinutes * 60, request.Scopes));
    }
}
