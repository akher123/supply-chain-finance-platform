using System.Security.Cryptography;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using Konscious.Security.Cryptography;

namespace ScfPlatform.Modules.Iam.Infrastructure.Security;

/// <summary>
/// BC-01-IAM-and-UAM.md §9.2 <c>PasswordHasher</c> — a real argon2id adapter (§9.2: "PasswordHasher
/// and JwtSigner should be real... since they are the security core"). Encodes salt + hash into
/// the stored <see cref="PasswordHash.Value"/> as <c>{iterations}.{memoryKb}.{parallelism}.{saltBase64}.{hashBase64}</c>
/// so <see cref="Verify"/> can recompute with the exact parameters used at hash time (allows
/// tuning the work factor over time without invalidating existing hashes).
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 4;
    private const int DefaultMemoryKb = 65536; // 64 MB
    private const int DefaultParallelism = 2;

    public PasswordHash Hash(RawPassword raw)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(raw.Value, salt, DefaultIterations, DefaultMemoryKb, DefaultParallelism);

        var encoded = $"{DefaultIterations}.{DefaultMemoryKb}.{DefaultParallelism}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        return PasswordHash.Create(PasswordHash.Argon2Id, encoded).Value;
    }

    public bool Verify(RawPassword raw, PasswordHash stored)
    {
        if (stored.Algorithm != PasswordHash.Argon2Id)
        {
            return false;
        }

        var parts = stored.Value.Split('.');

        if (parts.Length != 5
            || !int.TryParse(parts[0], out var iterations)
            || !int.TryParse(parts[1], out var memoryKb)
            || !int.TryParse(parts[2], out var parallelism))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[3]);
        var expectedHash = Convert.FromBase64String(parts[4]);

        var actualHash = ComputeHash(raw.Value, salt, iterations, memoryKb, parallelism);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb,
        };

        return argon2.GetBytes(HashSize);
    }
}
