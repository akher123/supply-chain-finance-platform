using ScfPlatform.BuildingBlocks.Domain;
using MediatR;

namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Base class for inbound integration-event handlers that gives idempotency (Foundations
/// §6.3) for free: checks the inbox by <see cref="IntegrationEvent.EventId"/> before
/// processing, skips if already seen, and records the id after a successful
/// <see cref="HandleEvent"/>. A module's Application layer subscribes to another module's
/// event by deriving from this instead of implementing
/// <see cref="INotificationHandler{TNotification}"/> directly.
/// </summary>
public abstract class IdempotentIntegrationEventHandler<TEvent> : INotificationHandler<IntegrationEventNotification<TEvent>>
    where TEvent : IntegrationEvent
{
    private readonly IInboxStore _inboxStore;

    protected IdempotentIntegrationEventHandler(IInboxStore inboxStore)
    {
        _inboxStore = inboxStore;
    }

    public async Task Handle(IntegrationEventNotification<TEvent> notification, CancellationToken cancellationToken)
    {
        var eventId = notification.Payload.EventId;

        if (await _inboxStore.IsProcessedAsync(eventId, cancellationToken))
        {
            return;
        }

        await HandleEvent(notification.Payload, cancellationToken);
        await _inboxStore.MarkAsProcessedAsync(eventId, cancellationToken);
    }

    /// <summary>The module-specific reaction to the event — only ever invoked once per <see cref="IntegrationEvent.EventId"/>.</summary>
    protected abstract Task HandleEvent(TEvent integrationEvent, CancellationToken cancellationToken);
}
