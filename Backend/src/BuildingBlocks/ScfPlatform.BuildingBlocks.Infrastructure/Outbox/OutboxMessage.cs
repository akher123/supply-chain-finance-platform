namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// The standard `outbox_messages` table every module schema includes
/// (Handover_Packages/00-Shared-Foundations.md §6.5). Integration events are written here
/// in the same transaction as the aggregate change (§6.2); a background relay
/// (<see cref="OutboxRelayBackgroundService{TDbContext}"/>) publishes unprocessed rows and
/// stamps <see cref="ProcessedOnUtc"/>.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>The CLR type's <see cref="Type.FullName"/> of the integration event, used to resolve it back via <see cref="IntegrationEventTypeRegistry"/>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The integration event, serialized as JSON.</summary>
    public string Content { get; init; } = string.Empty;

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? Error { get; set; }
}
