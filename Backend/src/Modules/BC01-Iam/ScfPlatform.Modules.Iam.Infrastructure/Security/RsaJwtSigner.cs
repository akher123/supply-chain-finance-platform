using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using Microsoft.IdentityModel.Tokens;

namespace ScfPlatform.Modules.Iam.Infrastructure.Security;

/// <summary>
/// BC-01-IAM-and-UAM.md §9.2 <c>JwtSigner</c> — RS256, a dev key (§9.2: "a proper... RS256 signing
/// with a dev key"). One RSA key pair is generated per process (registered as a DI singleton) —
/// good enough for this exercise; production would load a persisted/rotated key from a vault.
/// </summary>
public sealed class RsaJwtSigner : IJwtSigner, IDisposable
{
    private const string PermissionClaimType = "permission";
    private const string ScopeClaimType = "scope";
    private const string SessionIdClaimType = "sid";

    private readonly RSA _rsa;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _handler = new();

    public RsaJwtSigner()
    {
        _rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(_rsa);
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public string SignAccessToken(AccessTokenSpec spec)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, spec.Subject.ToString()),
            new(ClaimTypes.Role, spec.Role.ToString()),
            new(SessionIdClaimType, spec.SessionId.ToString()),
        };

        claims.AddRange(spec.Permissions.Select(p => new Claim(PermissionClaimType, p)));
        claims.AddRange(spec.Scopes.Select(s => new Claim(ScopeClaimType, s)));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: spec.ExpiresOnUtc,
            signingCredentials: _signingCredentials);

        return _handler.WriteToken(token);
    }

    public (string RefreshToken, string RefreshTokenHash) IssueRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return (raw, HashRefreshToken(raw));
    }

    public bool ValidateSignature(string token)
    {
        try
        {
            _handler.ValidateToken(token, _validationParameters, out _);
            return true;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            // A malformed/garbage token (not just an otherwise-well-formed one with a bad
            // signature/expiry) throws ArgumentException/SecurityTokenMalformedException before
            // ValidateToken even gets to signature checking — still just "not a valid token".
            return false;
        }
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));

    public JwtClaims? ReadClaims(string token)
    {
        try
        {
            var jwt = _handler.ReadJwtToken(token);
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            var sessionId = jwt.Claims.FirstOrDefault(c => c.Type == SessionIdClaimType)?.Value;

            if (sub is null || role is null || sessionId is null)
            {
                return null;
            }

            var permissions = jwt.Claims.Where(c => c.Type == PermissionClaimType).Select(c => c.Value).ToList();

            return new JwtClaims(Guid.Parse(sub), role, permissions, Guid.Parse(sessionId), jwt.ValidTo);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    public void Dispose() => _rsa.Dispose();
}
