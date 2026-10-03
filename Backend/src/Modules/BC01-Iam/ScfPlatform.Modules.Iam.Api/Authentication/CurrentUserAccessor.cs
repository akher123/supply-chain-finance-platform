using System.Security.Claims;

namespace ScfPlatform.Modules.Iam.Api.Authentication;

/// <summary>Reads the authenticated caller's identity out of the <see cref="ClaimsPrincipal"/> the <see cref="BearerTokenAuthenticationHandler"/> built.</summary>
public static class CurrentUserAccessor
{
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid SessionId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(IamClaimTypes.SessionId)!);

    public static bool HasPermission(this ClaimsPrincipal principal, string permission) =>
        principal.HasClaim(IamClaimTypes.Permission, permission);
}
