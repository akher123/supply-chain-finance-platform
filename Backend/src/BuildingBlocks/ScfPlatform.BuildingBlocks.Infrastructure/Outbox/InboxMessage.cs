namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// The standard `inbox_messages` table every module schema includes
/// (Handover_Packages/00-Shared-Foundations.md §6.5). Every inbound integration-event
/// handler checks this table by <see cref="EventId"/> before processing, and records it
/// afterward, so delivering the same event twice is a no-op (§6.3). See
/// <see cref="IInboxStore"/> / <see cref="IdempotentIntegrationEventHandler{TEvent}"/>.
/// </summary>
public sealed class InboxMessage
{
    public Guid EventId { get; init; }

    public DateTime ProcessedOnUtc { get; init; }
}
