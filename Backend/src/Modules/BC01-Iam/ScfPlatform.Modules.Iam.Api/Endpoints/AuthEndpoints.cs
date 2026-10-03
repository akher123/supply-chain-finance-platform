using ScfPlatform.Modules.Iam.Api.Authentication;
using ScfPlatform.Modules.Iam.Application.Activation;
using ScfPlatform.Modules.Iam.Application.Login;
using ScfPlatform.Modules.Iam.Application.Mfa;
using ScfPlatform.Modules.Iam.Application.Tokens;
using ScfPlatform.Modules.Iam.Domain.Enums;
using MediatR;

namespace ScfPlatform.Modules.Iam.Api.Endpoints;

/// <summary>BC-01-IAM-and-UAM.md §12 — login, activation, token, OAuth, and logout routes. All anonymous except <c>/token/revoke</c>, <c>/logout</c>, <c>/logout-all</c>.</summary>
public static class AuthEndpoints
{
    public sealed record LoginRequest(string Identifier, string Password, SessionChannel Channel, string DeviceFingerprint, bool RememberMe);

    public sealed record MfaVerifyRequest(Guid UserId, string Code, SessionChannel Channel, string DeviceFingerprint, bool RememberMe, bool UseBackupCode);

    public sealed record ActivateRequest(Guid UserId, string Otp);

    public sealed record ResendActivationRequest(Guid UserId);

    public sealed record RefreshRequest(string RefreshToken);

    public sealed record RevokeRequest(Guid SessionId);

    public sealed record OAuthTokenRequest(string GrantType, string ClientId, string? ClientSecret, Guid? ResourceOwnerUserId, IReadOnlyList<string>? Scopes);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/identity/login", async (LoginRequest request, HttpContext http, ISender sender) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await sender.Send(new LoginWithCredentialsCommand(
                request.Identifier, request.Password, request.Channel, request.DeviceFingerprint, request.RememberMe, ip));

            if (result.IsSuccess && !result.Value.MfaRequired)
            {
                SessionCookie.Set(http, result.Value.AccessToken!, result.Value.AccessTokenExpiresOnUtc!.Value);
            }

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/mfa/verify", async (MfaVerifyRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new VerifyMfaChallengeCommand(
                request.UserId, request.Code, request.Channel, request.DeviceFingerprint, request.RememberMe, request.UseBackupCode));

            if (result.IsSuccess)
            {
                SessionCookie.Set(http, result.Value.AccessToken!, result.Value.AccessTokenExpiresOnUtc!.Value);
            }

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/activate", async (ActivateRequest request, ISender sender) =>
        {
            var result = await sender.Send(new ActivateAccountCommand(request.UserId, request.Otp));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/activate/resend", async (ResendActivationRequest request, ISender sender) =>
        {
            var result = await sender.Send(new ResendActivationOtpCommand(request.UserId));

            return result.IsSuccess ? Results.Accepted() : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/token/refresh", async (RefreshRequest request, ISender sender) =>
        {
            var result = await sender.Send(new RefreshAccessTokenCommand(request.RefreshToken));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/token/revoke", async (RevokeRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new RevokeTokenCommand(http.User.UserId(), request.SessionId));

            return result.IsSuccess ? Results.NoContent() : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        app.MapPost("/api/identity/oauth/token", async (OAuthTokenRequest request, ISender sender) =>
        {
            var result = await sender.Send(new IssueOAuthTokenCommand(
                request.GrantType, request.ClientId, request.ClientSecret, request.ResourceOwnerUserId, request.Scopes ?? []));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/logout", async (HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new LogoutCommand(http.User.UserId(), http.User.SessionId()));

            if (result.IsSuccess)
            {
                SessionCookie.Clear(http);
            }

            return result.IsSuccess ? Results.NoContent() : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        app.MapPost("/api/identity/logout-all", async (HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new LogoutAllSessionsCommand(http.User.UserId()));

            if (result.IsSuccess)
            {
                SessionCookie.Clear(http);
            }

            return result.IsSuccess ? Results.NoContent() : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        return app;
    }
}
