using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScfPlatform.BuildingBlocks.Application;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence.Configurations;

/// <summary>BC-01-IAM-and-UAM.md §11.1 <c>revoked_tokens</c>.</summary>
public sealed class RevokedTokenRecordConfiguration : IEntityTypeConfiguration<RevokedTokenRecord>
{
    public void Configure(EntityTypeBuilder<RevokedTokenRecord> builder)
    {
        builder.ToTable("RevokedTokenRecords", ModuleSchemas.Iam);
        builder.HasKey(r => r.TokenIdOrRefreshHash);
        builder.Property(r => r.TokenIdOrRefreshHash).HasColumnName("TokenIdOrRefreshHash");
        builder.Property(r => r.RevokedOnUtc).HasColumnName("RevokedOnUtc");
        builder.Property(r => r.ExpiresOnUtc).HasColumnName("ExpiresOnUtc");
    }
}
