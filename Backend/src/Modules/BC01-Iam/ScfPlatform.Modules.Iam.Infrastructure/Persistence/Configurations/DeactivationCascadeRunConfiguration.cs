using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence.Configurations;

/// <summary>BC-01-IAM-and-UAM.md §8.3 <c>deactivation_cascade_runs</c> — BC-1-internal, not part of §11.1's reference schema (added by this module's own resolved design). No FK to <c>user_accounts</c>, consistent with this module's no-cross-aggregate-FK convention.</summary>
public sealed class DeactivationCascadeRunConfiguration : IEntityTypeConfiguration<DeactivationCascadeRun>
{
    public void Configure(EntityTypeBuilder<DeactivationCascadeRun> builder)
    {
        builder.ToTable("DeactivationCascadeRuns", ModuleSchemas.Iam);
        builder.UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(StronglyTypedIdValueConverter.Create(v => new DeactivationCascadeRunId(v))).HasColumnName("Id").ValueGeneratedNever();

        builder.Property(r => r.UserId)
            .HasConversion(StronglyTypedIdValueConverter.Create(v => new UserAccountId(v)))
            .HasColumnName("UserId")
            .IsRequired();

        builder.Property(r => r.StartedOnUtc).HasColumnName("StartedOnUtc");
        builder.Property(r => r.DeadlineUtc).HasColumnName("DeadlineUtc");
        builder.Property(r => r.Status).HasColumnName("Status").HasConversion<string>().IsRequired();

        builder.Property(r => r.ReceivedSignals)
            .HasColumnName("ReceivedSignals")
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(ListValueComparers.StringList);

        // Fixed, identical for every run (§8.3) — never varies, so not persisted.
        builder.Ignore(r => r.ExpectedSignals);
        builder.Ignore(r => r.MissingSignals);

        builder.Property<byte[]>("RowVersion").HasColumnName("RowVersion").IsRowVersion();

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.Status);
    }
}
