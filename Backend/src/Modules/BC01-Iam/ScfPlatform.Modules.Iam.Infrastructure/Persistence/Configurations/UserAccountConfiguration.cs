using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence.Configurations;

/// <summary>
/// BC-01-IAM-and-UAM.md §11.1 <c>user_accounts</c> (+ its owned child tables). Uses
/// <see cref="PropertyAccessMode.Field"/> throughout — the aggregate exposes its collections as
/// computed <c>IReadOnlyList&lt;T&gt;</c> properties over private backing fields (per Domain's
/// encapsulation rules), which is the standard/documented EF Core pattern for that shape: EF
/// reads/writes the field directly, never the property getter.
/// </summary>
public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts", ModuleSchemas.Iam);
        builder.UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(StronglyTypedIdValueConverter.Create(v => new UserAccountId(v)))
            .HasColumnName("Id")
            .ValueGeneratedNever();

        // Credential is split into scalar columns (§11.2) — email/mobile/role/status are queried
        // or uniquely constrained, so they cannot stay inside a json blob.
        builder.OwnsOne(a => a.Credential, credential =>
        {
            credential.OwnsOne(c => c.Email, email =>
            {
                email.Property(e => e.Value).HasColumnName("Email").IsRequired();
                email.HasIndex(e => e.Value).IsUnique();
            });

            credential.OwnsOne(c => c.Mobile, mobile =>
            {
                mobile.Property(m => m.Value).HasColumnName("Mobile").IsRequired();
                mobile.HasIndex(m => m.Value).IsUnique();
            });

            credential.OwnsOne(c => c.PasswordHash, hash =>
            {
                hash.Property(h => h.Algorithm).HasColumnName("PasswordAlgorithm").IsRequired();
                hash.Property(h => h.Value).HasColumnName("PasswordHash").IsRequired();
            });
        });

        builder.Property(a => a.Role).HasColumnName("Role").HasConversion<string>().IsRequired();
        builder.Property(a => a.Status).HasColumnName("Status").HasConversion<string>().IsRequired();
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Role);

        builder.Property(a => a.PasswordHistory)
            .HasColumnName("PasswordHistory")
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(ListValueComparers.StringList);

        builder.Property(a => a.Permissions)
            .HasColumnName("Permissions")
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(ListValueComparers.StringList);

        builder.OwnsOne(a => a.LockState, lockState =>
        {
            lockState.Property(l => l.IsLocked).HasColumnName("IsLocked");
            lockState.Property(l => l.LockedUntilUtc).HasColumnName("LockedUntilUtc");
            lockState.Property(l => l.FailedLoginCount).HasColumnName("FailedLoginCount");
            lockState.Property(l => l.FailedOtpCount).HasColumnName("FailedOtpCount");
        });

        builder.OwnsOne(a => a.Mfa, mfa =>
        {
            mfa.Property(m => m.Enabled).HasColumnName("Mfa_Enabled");
            mfa.Property(m => m.Method).HasColumnName("Mfa_Method").HasConversion<string>();
            mfa.Property(m => m.SecretRef).HasColumnName("Mfa_SecretRef");
        });

        builder.Property(a => a.IdentityVerified).HasColumnName("IdentityVerified");
        builder.Property(a => a.SuspendedReason).HasColumnName("SuspendedReason");
        builder.Property(a => a.ActivatedOnUtc).HasColumnName("ActivatedOnUtc");
        builder.Property(a => a.DeactivatedOnUtc).HasColumnName("DeactivatedOnUtc");
        builder.Property(a => a.CreatedOnUtc).HasColumnName("CreatedOnUtc");
        builder.Property(a => a.UpdatedOnUtc).HasColumnName("UpdatedOnUtc");

        // §11.1's "version_token" is modeled as a real SQL Server rowversion/timestamp shadow column (row_version), not
        // a mapped app column — nothing needs to increment it by hand, unlike a plain int field.
        builder.Property<byte[]>("RowVersion").HasColumnName("RowVersion").IsRowVersion();

        ConfigureSessions(builder);
        ConfigureTrustedDevices(builder);
        ConfigureBackupCodes(builder);
        ConfigurePasswordResetTokens(builder);
    }

    private static void ConfigureSessions(EntityTypeBuilder<UserAccount> builder)
    {
        builder.OwnsMany(a => a.Sessions, sessions =>
        {
            sessions.ToTable("Sessions", ModuleSchemas.Iam);
            sessions.UsePropertyAccessMode(PropertyAccessMode.Field);
            sessions.WithOwner().HasForeignKey("UserAccountId");
            sessions.HasKey(s => s.Id);
            sessions.Property(s => s.Id).HasConversion(StronglyTypedIdValueConverter.Create(v => new SessionId(v))).HasColumnName("Id").ValueGeneratedNever();
            sessions.Property(s => s.Channel).HasColumnName("Channel").HasConversion<string>().IsRequired();
            sessions.OwnsOne(s => s.DeviceFingerprint, fp => fp.Property(f => f.Value).HasColumnName("DeviceFingerprint").IsRequired());
            sessions.Property(s => s.RefreshTokenHash).HasColumnName("RefreshTokenHash").IsRequired();
            sessions.Property(s => s.IssuedOnUtc).HasColumnName("IssuedOnUtc");
            sessions.Property(s => s.LastSeenUtc).HasColumnName("LastSeenUtc");
            sessions.Property(s => s.ExpiresOnUtc).HasColumnName("ExpiresOnUtc");
            sessions.Property(s => s.RevokedOnUtc).HasColumnName("RevokedOnUtc");
            sessions.HasIndex("UserAccountId");
            sessions.HasIndex(s => s.RefreshTokenHash);
        });
    }

    private static void ConfigureTrustedDevices(EntityTypeBuilder<UserAccount> builder)
    {
        builder.OwnsMany(a => a.TrustedDevices, devices =>
        {
            devices.ToTable("TrustedDevices", ModuleSchemas.Iam);
            devices.UsePropertyAccessMode(PropertyAccessMode.Field);
            devices.WithOwner().HasForeignKey("UserAccountId");
            devices.HasKey(d => d.Id);
            devices.Property(d => d.Id).HasConversion(StronglyTypedIdValueConverter.Create(v => new TrustedDeviceId(v))).HasColumnName("Id").ValueGeneratedNever();
            devices.OwnsOne(d => d.DeviceFingerprint, fp => fp.Property(f => f.Value).HasColumnName("DeviceFingerprint").IsRequired());
            devices.Property(d => d.Label).HasColumnName("Label").IsRequired();
            devices.Property(d => d.TrustedUntilUtc).HasColumnName("TrustedUntilUtc");
            devices.HasIndex("UserAccountId");
        });
    }

    private static void ConfigureBackupCodes(EntityTypeBuilder<UserAccount> builder)
    {
        builder.OwnsMany(a => a.BackupCodes, codes =>
        {
            codes.ToTable("BackupCodes", ModuleSchemas.Iam);
            codes.UsePropertyAccessMode(PropertyAccessMode.Field);
            codes.WithOwner().HasForeignKey("UserAccountId");
            codes.HasKey(c => c.Id);
            codes.Property(c => c.Id).HasConversion(StronglyTypedIdValueConverter.Create(v => new BackupCodeId(v))).HasColumnName("Id").ValueGeneratedNever();
            codes.Property(c => c.CodeHash).HasColumnName("CodeHash").IsRequired();
            codes.Property(c => c.UsedOnUtc).HasColumnName("UsedOnUtc");
            codes.HasIndex("UserAccountId");
        });
    }

    private static void ConfigurePasswordResetTokens(EntityTypeBuilder<UserAccount> builder)
    {
        builder.OwnsMany(a => a.PasswordResetTokens, tokens =>
        {
            tokens.ToTable("PasswordResetTokens", ModuleSchemas.Iam);
            tokens.UsePropertyAccessMode(PropertyAccessMode.Field);
            tokens.WithOwner().HasForeignKey("UserAccountId");
            tokens.HasKey(t => t.Id);
            tokens.Property(t => t.Id).HasConversion(StronglyTypedIdValueConverter.Create(v => new PasswordResetTokenId(v))).HasColumnName("Id").ValueGeneratedNever();
            tokens.Property(t => t.TokenHash).HasColumnName("TokenHash").IsRequired();
            tokens.Property(t => t.IssuedOnUtc).HasColumnName("IssuedOnUtc");
            tokens.Property(t => t.ExpiresOnUtc).HasColumnName("ExpiresOnUtc");
            tokens.Property(t => t.UsedOnUtc).HasColumnName("UsedOnUtc");
            tokens.HasIndex("UserAccountId");
            tokens.HasIndex(t => t.TokenHash);
        });
    }
}
