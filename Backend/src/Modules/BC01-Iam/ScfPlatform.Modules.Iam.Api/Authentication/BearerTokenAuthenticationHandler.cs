using System.Security.Claims;
using System.Text.Encodings.Web;
using ScfPlatform.Modules.Iam.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScfPlatform.Modules.Iam.Api.Authentication;

namespace ScfPlatform.Modules.Iam.Api.Authentication;

/// <summary>
/// BC-01-IAM-and-UAM.md §9.3: "Used by the HOST's authentication middleware... to check a token
/// out-of-band." No host-wide authentication existed before this module's own unit (B0 left it
/// unset), so this is that middleware — a thin ASP.NET Core <see cref="AuthenticationHandler{TOptions}"/>
/// wrapping <see cref="ITokenValidationApi"/> (never re-implementing JWT validation itself).
/// </summary>
public sealed class BearerTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    private readonly ITokenValidationApi _tokenValidationApi;

    public BearerTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenValidationApi tokenValidationApi)
        : base(options, logger, encoder)
    {
        _tokenValidationApi = tokenValidationApi;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();
        var result = await _tokenValidationApi.Validate(token);

        if (result.IsFailure)
        {
            return AuthenticateResult.Fail(result.Error.Message);
        }

        var principalData = result.Value;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, principalData.UserId.ToString()),
            new(ClaimTypes.Role, principalData.Role),
            new(IamClaimTypes.SessionId, principalData.SessionId.ToString()),
        };

        claims.AddRange(principalData.Permissions.Select(p => new Claim(IamClaimTypes.Permission, p)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

public static class IamClaimTypes
{
    public const string Permission = "permission";
    public const string SessionId = "sid";
}
