using ScfPlatform.Modules.Iam.Api.Endpoints;

namespace ScfPlatform.Modules.Iam.Api;

/// <summary>
/// Maps the Iam module's HTTP endpoints (BC-01-IAM-and-UAM.md §12) onto the shared host. Called
/// once from the host's composition root alongside every other module's Map*Endpoints. Route
/// prefix <c>/api/identity</c>. Public endpoints (login, MFA verify, activate/resend, token
/// refresh, OAuth token, password reset request/verify/complete) are anonymous; everything else
/// requires a valid access token (<see cref="Authentication.BearerTokenAuthenticationHandler"/>);
/// admin endpoints additionally require the <c>users:manage</c> permission.
/// </summary>
public static class IamEndpointMapping
{
    public static IEndpointRouteBuilder MapIamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapMfaEndpoints();
        app.MapPasswordEndpoints();
        app.MapMeEndpoints();
        app.MapAdminEndpoints();

        // TokenValidationApi is deliberately NOT mapped as an HTTP route (§12) — it's the
        // in-process contract BearerTokenAuthenticationHandler calls directly.
        return app;
    }
}
