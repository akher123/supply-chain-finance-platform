using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Events;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Domain.Aggregates;

/// <summary>
/// BC-01-IAM-and-UAM.md §5.2, §6.3. Aggregate root, kept separate from <see cref="UserAccount"/>
/// because it has its own short, high-churn lifecycle (<c>Issued → Verified | Expired | Locked</c>)
/// and is created during provisioning *before* the account is something the user can act on.
/// Even though both aggregates live in the same module schema, <see cref="UserAccountId"/> below
/// is a plain reference — no FK is drawn between the two aggregate tables (§11.2).
/// </summary>
public sealed class OtpChallenge : AggregateRoot<OtpChallengeId>
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    // ORM materialization only.
    private OtpChallenge()
    {
    }

    private OtpChallenge(
        OtpChallengeId id,
        UserAccountId userAccountId,
        OtpPurpose purpose,
        string codeHash,
        int maxAttempts,
        DateTime issuedOnUtc,
        DateTime expiresOnUtc)
        : base(id)
    {
        UserAccountId = userAccountId;
        Purpose = purpose;
        CodeHash = codeHash;
        Status = OtpStatus.Issued;
        AttemptCount = 0;
        MaxAttempts = maxAttempts;
        IssuedOnUtc = issuedOnUtc;
        ExpiresOnUtc = expiresOnUtc;
        VerifiedOnUtc = null;
    }

    public UserAccountId UserAccountId { get; private set; } = null!;

    public OtpPurpose Purpose { get; private set; }

    /// <summary>A hash of the 6-digit code — the plaintext is never stored (§6.2 OtpChallenge invariants).</summary>
    public string CodeHash { get; private set; } = string.Empty;

    public OtpStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTime IssuedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? VerifiedOnUtc { get; private set; }

    /// <summary>§6.3. Max attempts: 5 for <see cref="OtpPurpose.Activation"/>, 3 for <see cref="OtpPurpose.Mfa"/>/<see cref="OtpPurpose.PasswordReset"/> (the caller supplies the right value; see §6.2 invariant #5).</summary>
    public static Result<OtpChallenge> Issue(
        UserAccountId userAccountId,
        OtpPurpose purpose,
        string codeHash,
        DateTime nowUtc,
        int maxAttempts,
        TimeSpan? ttl = null)
    {
        ArgumentNullException.ThrowIfNull(userAccountId);

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            return Result.Failure<OtpChallenge>(Error.Validation("E-OTP-CODE-HASH-REQUIRED", "A code hash is required to issue an OTP challenge."));
        }

        if (maxAttempts <= 0)
        {
            return Result.Failure<OtpChallenge>(Error.Validation("E-OTP-MAX-ATTEMPTS-INVALID", "Max attempts must be positive."));
        }

        var expiresOnUtc = nowUtc.Add(ttl ?? DefaultTtl);

        var challenge = new OtpChallenge(OtpChallengeId.New(), userAccountId, purpose, codeHash, maxAttempts, nowUtc, expiresOnUtc);
        challenge.Raise(new OtpIssuedDomainEvent(challenge.Id, userAccountId, purpose, expiresOnUtc));

        return Result.Success(challenge);
    }

    /// <summary>
    /// §6.3. Only legal from <see cref="OtpStatus.Issued"/>. Past expiry → <see cref="OtpStatus.Expired"/>,
    /// fails <c>E-OTP-EXPIRED</c>. A hash mismatch increments <see cref="AttemptCount"/>; at
    /// <see cref="MaxAttempts"/> the challenge locks (<c>E-OTP-LOCKED</c>). A match → <see cref="OtpStatus.Verified"/>.
    /// </summary>
    public Result Verify(string submittedCodeHash, DateTime nowUtc)
    {
        if (Status != OtpStatus.Issued)
        {
            return Result.Failure(Error.Conflict("E-OTP-NOT-PENDING", $"This OTP challenge is '{Status}' and cannot be verified again."));
        }

        if (nowUtc > ExpiresOnUtc)
        {
            Status = OtpStatus.Expired;
            return Result.Failure(Error.Validation("E-OTP-EXPIRED", "This OTP challenge has expired."));
        }

        if (!string.Equals(submittedCodeHash, CodeHash, StringComparison.Ordinal))
        {
            AttemptCount++;

            if (AttemptCount >= MaxAttempts)
            {
                Status = OtpStatus.Locked;
                return Result.Failure(Error.Failure("E-OTP-LOCKED", "Too many incorrect attempts — this OTP challenge is locked."));
            }

            return Result.Failure(Error.Validation("E-OTP-INVALID", "The submitted code does not match."));
        }

        Status = OtpStatus.Verified;
        VerifiedOnUtc = nowUtc;
        Raise(new OtpVerifiedDomainEvent(Id, UserAccountId, Purpose));

        return Result.Success();
    }

    /// <summary>Invoked by the OTP-expiry sweep job (§3) — moves a stale <see cref="OtpStatus.Issued"/> challenge to <see cref="OtpStatus.Expired"/>. A no-op if already past <see cref="OtpStatus.Issued"/>.</summary>
    public void MarkExpired()
    {
        if (Status == OtpStatus.Issued)
        {
            Status = OtpStatus.Expired;
        }
    }
}
