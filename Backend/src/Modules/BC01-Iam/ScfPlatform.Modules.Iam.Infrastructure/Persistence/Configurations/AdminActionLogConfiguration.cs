using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.Modules.Iam.Domain.Entities;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence.Configurations;

/// <summary>BC-01-IAM-and-UAM.md §11.1 <c>admin_action_log</c> — append-only.</summary>
public sealed class AdminActionLogConfiguration : IEntityTypeConfiguration<AdminActionLog>
{
    public void Configure(EntityTypeBuilder<AdminActionLog> builder)
    {
        builder.ToTable("AdminActionLog", ModuleSchemas.Iam);
        builder.UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("Id").ValueGeneratedNever();
        builder.Property(e => e.AdminUserId).HasColumnName("AdminUserId").IsRequired();
        builder.Property(e => e.ActionType).HasColumnName("ActionType").HasConversion<string>().IsRequired();
        builder.Property(e => e.TargetUserId).HasColumnName("TargetUserId").IsRequired();
        builder.Property(e => e.Reason).HasColumnName("Reason");
        builder.Property(e => e.OccurredOnUtc).HasColumnName("OccurredOnUtc");

        builder.HasIndex(e => e.TargetUserId);
        builder.HasIndex(e => e.AdminUserId);
        builder.HasIndex(e => e.OccurredOnUtc);
    }
}
