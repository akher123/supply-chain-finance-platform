using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Maps the standard <c>outbox_messages</c> / <c>inbox_messages</c> tables
/// (Foundations §6.5) into a module's own schema. Every module's <c>DbContext</c> (built in
/// that module's own unit) calls this once from <c>OnModelCreating</c>:
/// <code>modelBuilder.ConfigureOutboxAndInbox(ModuleSchemas.Iam);</code>
/// </summary>
public static class OutboxInboxModelBuilderExtensions
{
    public static ModelBuilder ConfigureOutboxAndInbox(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages", schema);
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Id).HasColumnName("Id");
            builder.Property(message => message.Type).HasColumnName("Type").IsRequired();
            builder.Property(message => message.Content).HasColumnName("Content").IsRequired();
            builder.Property(message => message.OccurredOnUtc).HasColumnName("OccurredOnUtc").IsRequired();
            builder.Property(message => message.ProcessedOnUtc).HasColumnName("ProcessedOnUtc");
            builder.Property(message => message.Error).HasColumnName("Error");
            builder.HasIndex(message => message.ProcessedOnUtc).HasDatabaseName("ProcessedOnUtc");
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("InboxMessages", schema);
            builder.HasKey(message => message.EventId);
            builder.Property(message => message.EventId).HasColumnName("EventId");
            builder.Property(message => message.ProcessedOnUtc).HasColumnName("ProcessedOnUtc").IsRequired();
        });

        return modelBuilder;
    }
}
