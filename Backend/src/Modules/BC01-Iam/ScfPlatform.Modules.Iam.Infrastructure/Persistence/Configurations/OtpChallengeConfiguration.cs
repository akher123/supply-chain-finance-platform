using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence.Configurations;

/// <summary>BC-01-IAM-and-UAM.md §11.1 <c>otp_challenges</c> — a separate aggregate; <c>user_account_id</c> is a plain uuid, no FK to <c>user_accounts</c> (§11.2).</summary>
public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges", ModuleSchemas.Iam);
        builder.UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(StronglyTypedIdValueConverter.Create(v => new OtpChallengeId(v))).HasColumnName("Id").ValueGeneratedNever();

        builder.Property(c => c.UserAccountId)
            .HasConversion(StronglyTypedIdValueConverter.Create(v => new UserAccountId(v)))
            .HasColumnName("UserAccountId")
            .IsRequired();

        builder.Property(c => c.Purpose).HasColumnName("Purpose").HasConversion<string>().IsRequired();
        builder.Property(c => c.CodeHash).HasColumnName("CodeHash").IsRequired();
        builder.Property(c => c.Status).HasColumnName("Status").HasConversion<string>().IsRequired();
        builder.Property(c => c.AttemptCount).HasColumnName("AttemptCount");
        builder.Property(c => c.MaxAttempts).HasColumnName("MaxAttempts");
        builder.Property(c => c.IssuedOnUtc).HasColumnName("IssuedOnUtc");
        builder.Property(c => c.ExpiresOnUtc).HasColumnName("ExpiresOnUtc");
        builder.Property(c => c.VerifiedOnUtc).HasColumnName("VerifiedOnUtc");
        builder.Property<byte[]>("RowVersion").HasColumnName("RowVersion").IsRowVersion();

        builder.HasIndex(c => new { c.UserAccountId, c.Purpose });
    }
}
