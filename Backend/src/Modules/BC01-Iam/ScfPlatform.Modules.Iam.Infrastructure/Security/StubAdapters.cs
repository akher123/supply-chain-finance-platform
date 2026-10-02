using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ScfPlatform.Modules.Iam.Infrastructure.Security;

/// <summary>
/// BC-01-IAM-and-UAM.md §9.2 — "For the exercise, Infrastructure may provide stub adapters for
/// BreachCheckPort... OtpDeliveryPort... RateLimiterPort... and TotpProvider." Keep the port
/// shapes exactly as documented so production adapters drop in later.
/// </summary>

/// <summary>In-memory blocklist standing in for a k-anonymity breach-corpus lookup.</summary>
public sealed class InMemoryBreachCheckPort : IBreachCheckPort
{
    private static readonly HashSet<string> KnownBreachedPasswords = new(StringComparer.Ordinal)
    {
        "password123", "123456789012", "qwertyuiop123", "letmein12345", "admin12345678",
    };

    public Task<bool> IsBreachedAsync(RawPassword raw, CancellationToken cancellationToken) =>
        Task.FromResult(KnownBreachedPasswords.Contains(raw.Value));
}

/// <summary>Logs the OTP instead of sending it over a real SMS/email transport.</summary>
public sealed class LoggingOtpDeliveryPort : IOtpDeliveryPort
{
    private readonly ILogger<LoggingOtpDeliveryPort> _logger;

    public LoggingOtpDeliveryPort(ILogger<LoggingOtpDeliveryPort> logger) => _logger = logger;

    public Task<Result> SendAsync(string destination, string plaintextCode, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OTP for {Purpose} to {Destination}: {Code}", purpose, destination, plaintextCode);

        return Task.FromResult(Result.Success());
    }
}

/// <summary>A fixed-window in-memory rate limiter — good enough for a single-process exercise; production would use a distributed counter (Redis, etc.).</summary>
public sealed class InMemoryRateLimiterPort : IRateLimiterPort
{
    private sealed record Window(int Count, DateTime WindowStartUtc);

    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public Task<bool> TryConsumeAsync(string key, int maxInWindow, TimeSpan window, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var result = _windows.AddOrUpdate(
            key,
            _ => new Window(1, now),
            (_, existing) => now - existing.WindowStartUtc > window
                ? new Window(1, now)
                : existing with { Count = existing.Count + 1 });

        return Task.FromResult(result.Count <= maxInWindow);
    }
}

/// <summary>RFC 6238 TOTP (30s step, 6 digits, HMAC-SHA1) — a real, self-contained implementation; no external secret vault, so <c>SecretRef</c> is the base32 secret itself.</summary>
public sealed class Rfc6238TotpProvider : ITotpProvider
{
    private const int StepSeconds = 30;
    private const int Digits = 6;

    public (string SecretRef, string ProvisioningUri) Enroll(string accountLabel)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(20);
        var secret = Base32Encode(secretBytes);
        var uri = $"otpauth://totp/CogniJobs:{Uri.EscapeDataString(accountLabel)}?secret={secret}&issuer=CogniJobs&digits={Digits}&period={StepSeconds}";

        return (secret, uri);
    }

    public bool Verify(string secretRef, string submittedCode)
    {
        var secretBytes = Base32Decode(secretRef);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

        // Accept the current step and one step of clock drift either side.
        for (var drift = -1; drift <= 1; drift++)
        {
            if (ComputeCode(secretBytes, counter + drift) == submittedCode)
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeCode(byte[] secret, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binaryCode % (int)Math.Pow(10, Digits)).ToString($"D{Digits}");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var builder = new StringBuilder();
        int bitBuffer = 0, bitCount = 0;

        foreach (var b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                builder.Append(alphabet[(bitBuffer >> (bitCount - 5)) & 0x1F]);
                bitCount -= 5;
            }
        }

        if (bitCount > 0)
        {
            builder.Append(alphabet[(bitBuffer << (5 - bitCount)) & 0x1F]);
        }

        return builder.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int bitBuffer = 0, bitCount = 0;

        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(c);

            if (index < 0)
            {
                continue;
            }

            bitBuffer = (bitBuffer << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bytes.Add((byte)((bitBuffer >> (bitCount - 8)) & 0xFF));
                bitCount -= 8;
            }
        }

        return bytes.ToArray();
    }
}
