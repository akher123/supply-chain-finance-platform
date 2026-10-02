using ScfPlatform.BuildingBlocks.Domain;
namespace ScfPlatform.Modules.Iam.Domain.Ids;


public sealed record UserAccountId(Guid Value) : StronglyTypedId(Value)
{
    public static UserAccountId New() => new(Guid.NewGuid());
}

public sealed record OtpChallengeId(Guid Value): StronglyTypedId(Value)
{
    public static OtpChallengeId New()=> new(Guid.NewGuid());
}

public sealed record SessionId(Guid Value): StronglyTypedId(Value)
{
    public static SessionId New()=> new(Guid.NewGuid());
}

public sealed record TrustedDeviceId(Guid Value) : StronglyTypedId(Value)
{
    public static TrustedDeviceId New()=>new(Guid.NewGuid());
}

public sealed record BackupCodeId(Guid Value) : StronglyTypedId(Value)
{
    public static BackupCodeId New()=>new(Guid.NewGuid());
}

public sealed record PasswordResetTokenId(Guid Value) : StronglyTypedId(Value)
{
    public static PasswordResetTokenId New()=>new(Guid.NewGuid());
}

public sealed record DeactivationCascadeRunId(Guid Value) : StronglyTypedId(Value)
{
    public static DeactivationCascadeRunId New() => new(Guid.NewGuid());
}