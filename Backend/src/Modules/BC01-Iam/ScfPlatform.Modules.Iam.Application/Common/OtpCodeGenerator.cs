using System.Security.Cryptography;
using System.Text;

namespace ScfPlatform.Modules.Iam.Application.Common;

/// <summary>
/// Generates the plaintext 6-digit OTP code (BC-01-IAM-and-UAM.md §2) and its hash. Not a §9.2
/// port — unlike password hashing (which needs a configurable, upgradeable algorithm behind
/// <c>PasswordHasher</c>), OTP hashing here is a fixed, non-configurable SHA-256 digest, so a
/// plain static helper is enough; no adapter indirection needed.
/// </summary>
public static class OtpCodeGenerator
{
    public static (string PlaintextCode, string CodeHash) Generate()
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        return (code, Hash(code));
    }

    public static string Hash(string plaintextCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextCode)));
}
