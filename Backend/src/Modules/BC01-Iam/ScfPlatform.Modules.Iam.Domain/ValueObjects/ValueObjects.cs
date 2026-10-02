using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using System.Text.RegularExpressions;

namespace ScfPlatform.Modules.Iam.Domain.ValueObjects;

public sealed class EmailAddress : ValueObject
{
    // Pragmatic RFC 5322 approximation (the same one most production validators use — full RFC
    // 5322 grammar is famously impractical to express as a single regex).
    private static readonly Regex Pattern = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<EmailAddress> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Pattern.IsMatch(value.Trim()))
        {
            return Result.Failure<EmailAddress>(
                Error.Validation("E-REG-INVALID-EMAIL", "Email address is not a valid RFC 5322 address."));
        }

        return Result.Success(new EmailAddress(value.Trim().ToLowerInvariant()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
public sealed class MobileNumber : ValueObject
{
    private const string DefaultCountryCode = "880";

    private static readonly Regex E164Pattern = new(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled);

    private MobileNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<MobileNumber> Create(string? value, string defaultCountryCode = DefaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<MobileNumber>(
                Error.Validation("E-REG-INVALID-MOBILE", "Mobile number is required."));
        }

        var trimmed = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);

        // Normalize a locally formatted number (e.g. "01812345678") to E.164 using the default
        // region — this module's default region is Bangladesh (+880).
        if (!trimmed.StartsWith('+'))
        {
            trimmed = trimmed.TrimStart('0');
            trimmed = $"+{defaultCountryCode}{trimmed}";
        }

        if (!E164Pattern.IsMatch(trimmed))
        {
            return Result.Failure<MobileNumber>(
                Error.Validation("E-REG-INVALID-MOBILE", "Mobile number is not a valid E.164 number."));
        }

        return Result.Success(new MobileNumber(trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
public sealed class PasswordHash : ValueObject
{
    public const string Argon2Id = "argon2id";

    private PasswordHash(string algorithm, string value)
    {
        Algorithm = algorithm;
        Value = value;
    }

    public string Algorithm { get; }

    public string Value { get; }

    public static Result<PasswordHash> Create(string algorithm, string value)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            return Result.Failure<PasswordHash>(Error.Validation("E-PASSWORD-HASH-INVALID", "Hash algorithm is required."));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PasswordHash>(Error.Validation("E-PASSWORD-HASH-INVALID", "Hash value is required."));
        }

        return Result.Success(new PasswordHash(algorithm, value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Algorithm;
        yield return Value;
    }
}

public sealed class RawPassword : ValueObject
{
    private RawPassword(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<RawPassword> Create(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result.Failure<RawPassword>(Error.Validation("E-PASSWORD-REQUIRED", "Password is required."));
        }

        return Result.Success(new RawPassword(value));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public sealed class Credential : ValueObject
{
    // EF Core materialization only. Constructor binding can't be used for this shell: its
    // constructor params (EmailAddress/MobileNumber/PasswordHash) are themselves owned types,
    // not scalars, which EF Core's constructor-injection materialization doesn't support — so
    // this type needs a parameterless constructor + settable properties instead (its three
    // nested VOs still materialize via their own scalar constructors as normal).
    private Credential()
    {
    }

    private Credential(EmailAddress email, MobileNumber mobile, PasswordHash passwordHash)
    {
        Email = email;
        Mobile = mobile;
        PasswordHash = passwordHash;
    }

    public EmailAddress Email { get; private set; } = null!;

    public MobileNumber Mobile { get; private set; } = null!;

    public PasswordHash PasswordHash { get; private set; } = null!;

    public static Result<Credential> Create(EmailAddress email, MobileNumber mobile, PasswordHash passwordHash)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(mobile);
        ArgumentNullException.ThrowIfNull(passwordHash);

        return Result.Success(new Credential(email, mobile, passwordHash));
    }

    /// <summary>
    /// Mutates <see cref="PasswordHash"/> in place — used by password change/reset. Real, found bug: EF Core's owned-within-owned change
    /// tracking (`Credential` owns `Email`/`Mobile`/`PasswordHash`, each themselves owned types, per
    /// `UserAccountConfiguration.cs`) loses `Email`/`Mobile`'s tracked values when the *parent*
    /// `Credential` reference is swapped for a new instance on `SaveChanges` — even though the new
    /// instance carries the same `Email`/`Mobile` object references — producing a `null value in
    /// column "email"` constraint violation on password reset/change. Mutating the existing tracked
    /// instance avoids the owned-reference replacement entirely.
    /// </summary>
    internal void ReplacePasswordHash(PasswordHash newPasswordHash) => PasswordHash = newPasswordHash;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Email;
        yield return Mobile;
        yield return PasswordHash;
    }
}

public sealed class LockState : ValueObject
{
    private LockState(bool isLocked, DateTime? lockedUntilUtc, int failedLoginCount, int failedOtpCount)
    {
        IsLocked = isLocked;
        LockedUntilUtc = lockedUntilUtc;
        FailedLoginCount = failedLoginCount;
        FailedOtpCount = failedOtpCount;
    }

    public bool IsLocked { get; }

    public DateTime? LockedUntilUtc { get; }

    public int FailedLoginCount { get; }

    public int FailedOtpCount { get; }

    public static LockState None => new(false, null, 0, 0);

    public static Result<LockState> Create(bool isLocked, DateTime? lockedUntilUtc, int failedLoginCount, int failedOtpCount)
    {
        if (failedLoginCount < 0 || failedOtpCount < 0)
        {
            return Result.Failure<LockState>(Error.Validation("E-LOCK-STATE-INVALID", "Failed-attempt counters cannot be negative."));
        }

        return Result.Success(new LockState(isLocked, lockedUntilUtc, failedLoginCount, failedOtpCount));
    }

    public LockState WithFailedLoginCount(int count) => new(IsLocked, LockedUntilUtc, count, FailedOtpCount);

    public LockState WithFailedOtpCount(int count) => new(IsLocked, LockedUntilUtc, FailedLoginCount, count);

    public LockState Locked(DateTime untilUtc) => new(true, untilUtc, FailedLoginCount, FailedOtpCount);

    public LockState Unlocked() => new(false, null, 0, 0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsLocked;
        yield return LockedUntilUtc;
        yield return FailedLoginCount;
        yield return FailedOtpCount;
    }
}

public sealed class MfaConfiguration : ValueObject
{
    private MfaConfiguration(bool enabled, MfaMethod method, string? secretRef)
    {
        Enabled = enabled;
        Method = method;
        SecretRef = secretRef;
    }

    public bool Enabled { get; }

    public MfaMethod Method { get; }

    public string? SecretRef { get; }

    public static MfaConfiguration Disabled => new(false, MfaMethod.None, null);

    public static Result<MfaConfiguration> Create(bool enabled, MfaMethod method, string? secretRef)
    {
        if (enabled && (method == MfaMethod.None || string.IsNullOrWhiteSpace(secretRef)))
        {
            return Result.Failure<MfaConfiguration>(
                Error.Validation("E-MFA-CONFIGURATION-INVALID", "An enabled MFA configuration requires a method and a secret reference."));
        }

        if (!enabled && method != MfaMethod.None)
        {
            return Result.Failure<MfaConfiguration>(
                Error.Validation("E-MFA-CONFIGURATION-INVALID", "A disabled MFA configuration cannot carry a method."));
        }

        return Result.Success(new MfaConfiguration(enabled, method, secretRef));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Enabled;
        yield return Method;
        yield return SecretRef;
    }
}

public sealed class AccessTokenSpec : ValueObject
{
    private AccessTokenSpec(
        Guid subject,
        UserRole role,
        IReadOnlyList<string> permissions,
        IReadOnlyList<string> scopes,
        Guid sessionId,
        DateTime expiresOnUtc)
    {
        Subject = subject;
        Role = role;
        Permissions = permissions;
        Scopes = scopes;
        SessionId = sessionId;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid Subject { get; }

    public UserRole Role { get; }

    public IReadOnlyList<string> Permissions { get; }

    public IReadOnlyList<string> Scopes { get; }

    public Guid SessionId { get; }

    public DateTime ExpiresOnUtc { get; }

    public static Result<AccessTokenSpec> Create(
        Guid subject,
        UserRole role,
        IReadOnlyList<string> permissions,
        IReadOnlyList<string> scopes,
        Guid sessionId,
        DateTime expiresOnUtc)
    {
        if (subject == Guid.Empty)
        {
            return Result.Failure<AccessTokenSpec>(Error.Validation("E-TOKEN-SPEC-INVALID", "Subject is required."));
        }

        if (sessionId == Guid.Empty)
        {
            return Result.Failure<AccessTokenSpec>(Error.Validation("E-TOKEN-SPEC-INVALID", "SessionId is required."));
        }

        return Result.Success(new AccessTokenSpec(subject, role, permissions, scopes, sessionId, expiresOnUtc));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Subject;
        yield return Role;
        yield return SessionId;
        yield return ExpiresOnUtc;

        foreach (var permission in Permissions)
        {
            yield return permission;
        }

        foreach (var scope in Scopes)
        {
            yield return scope;
        }
    }
}

public sealed class DeviceFingerprint : ValueObject
{
    private DeviceFingerprint(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<DeviceFingerprint> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<DeviceFingerprint>(Error.Validation("E-DEVICE-FINGERPRINT-INVALID", "Device fingerprint is required."));
        }

        return Result.Success(new DeviceFingerprint(value.Trim()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}