using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Services;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;

namespace ScfPlatform.Modules.Iam.Application.Login;

/// <summary>
/// Shared tail of every "credentials/second-factor verified — issue tokens" path
/// (BC-01-IAM-and-UAM.md §10.1 <c>LoginWithCredentialsCommand</c> and <c>VerifyMfaChallengeCommand</c>):
/// creates the <c>Session</c>, mints access + refresh tokens. Does not persist or enqueue the
/// <c>UserLoggedIn</c> integration event — the caller does that alongside its own unit-of-work save.
/// </summary>
public static class LoginTokenIssuer
{
    public const int AccessTokenTtlMinutes = 60;

    public static Result<LoginResult> IssueTokens(
        UserAccount account,
        SessionChannel channel,
        string deviceFingerprint,
        bool rememberMe,
        DateTime nowUtc,
        IJwtSigner jwtSigner)
    {
        var fingerprintResult = DeviceFingerprint.Create(deviceFingerprint);

        if (fingerprintResult.IsFailure)
        {
            return Result.Failure<LoginResult>(fingerprintResult.Error);
        }

        var (refreshToken, refreshTokenHash) = jwtSigner.IssueRefreshToken();
        var loginResult = account.RecordSuccessfulLogin(channel, fingerprintResult.Value, refreshTokenHash, nowUtc, rememberMe);

        if (loginResult.IsFailure)
        {
            return Result.Failure<LoginResult>(loginResult.Error);
        }

        var session = account.Sessions[^1];
        var specResult = TokenClaimsBuilder.BuildAccessToken(
            account.Id.Value, account.Role, account.Permissions, session.Id.Value, [], TimeSpan.FromMinutes(AccessTokenTtlMinutes), nowUtc);

        if (specResult.IsFailure)
        {
            return Result.Failure<LoginResult>(specResult.Error);
        }

        var accessToken = jwtSigner.SignAccessToken(specResult.Value);

        return Result.Success(new LoginResult(false, account.Id.Value, accessToken, refreshToken, session.Id.Value, specResult.Value.ExpiresOnUtc));
    }
}
