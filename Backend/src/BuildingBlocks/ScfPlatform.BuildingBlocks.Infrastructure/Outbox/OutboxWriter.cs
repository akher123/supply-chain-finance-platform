using System.Text.Json;
using ScfPlatform.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Enqueues an integration event onto the current unit of work's <c>outbox_messages</c> set.
/// Callers must still call <c>SaveChangesAsync</c> on the same <see cref="DbContext"/> that
/// changed the aggregate — that is what puts the outbox row in the *same transaction* as the
/// state change (Foundations §6.2). Never publishes anything directly.
/// </summary>
public static class OutboxWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static void Enqueue<TEvent>(DbContext context, TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Type = typeof(TEvent).FullName ?? typeof(TEvent).Name,
            Content = JsonSerializer.Serialize(integrationEvent, typeof(TEvent), SerializerOptions),
            OccurredOnUtc = integrationEvent.OccurredOnUtc,
        };

        context.Set<OutboxMessage>().Add(message);
    }
}
