namespace ScfPlatform.Modules.Iam.Api.Authentication;

/// <summary>
/// BC-01-IAM-and-UAM.md §12/§14.2 (US-3.1.5-01, US-3.1.5-04): "issue... a Session, set an
/// HTTP-only secure session cookie" / "stored server-side, HTTP-only secure cookie." This module's
/// primary auth mechanism is the bearer header (<see cref="BearerTokenAuthenticationHandler"/> —
/// simplest for the SPA/mobile-app-style clients every other endpoint in this contract assumes),
/// so the cookie here is a same-origin-browser convenience carrying the same access token, not a
/// second independently-read auth channel.
/// </summary>
public static class SessionCookie
{
    public const string Name = "Scf_platform_session";

    public static void Set(HttpContext http, string accessToken, DateTime expiresOnUtc)
    {
        http.Response.Cookies.Append(Name, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresOnUtc,
        });
    }

    public static void Clear(HttpContext http) => http.Response.Cookies.Delete(Name);
}
