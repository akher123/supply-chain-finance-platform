using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.BuildingBlocks.Infrastructure;
using ScfPlatform.BuildingBlocks.Infrastructure.Outbox;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>
/// This module's single persistence context / unit of work (00-Shared-Foundations.md §6.4).
/// Default schema <see cref="ModuleSchemas.Iam"/> (declared once, centrally, by B0 — the schema
/// BC-01-IAM-and-UAM.md §11.1 calls <c>identity_access</c> in its own text).
/// </summary>
public sealed class IamDbContext : DbContext
{
    public IamDbContext(DbContextOptions<IamDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<AdminActionLog> AdminActionLogs => Set<AdminActionLog>();

    public DbSet<DeactivationCascadeRun> DeactivationCascadeRuns => Set<DeactivationCascadeRun>();

    public DbSet<RevokedTokenRecord> RevokedTokens => Set<RevokedTokenRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ModuleSchemas.Iam);

        modelBuilder.ApplyConfiguration(new UserAccountConfiguration());
        modelBuilder.ApplyConfiguration(new OtpChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new AdminActionLogConfiguration());
        modelBuilder.ApplyConfiguration(new DeactivationCascadeRunConfiguration());
        modelBuilder.ApplyConfiguration(new RevokedTokenRecordConfiguration());

        modelBuilder.ConfigureOutboxAndInbox(ModuleSchemas.Iam);
        modelBuilder.IgnoreDomainEvents();
    }
}
