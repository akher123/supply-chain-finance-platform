using MediatR;
using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Adapts a deserialized <see cref="IntegrationEvent"/> (read back off the outbox by the
/// relay) into an in-process MediatR notification. A module's Application layer subscribes
/// by implementing <c>INotificationHandler&lt;IntegrationEventNotification&lt;TEvent&gt;&gt;</c>
/// — or, to get inbox idempotency for free, by deriving from
/// <see cref="IdempotentIntegrationEventHandler{TEvent}"/> (Foundations §6.3).
/// </summary>
public sealed record IntegrationEventNotification<TEvent>(TEvent Payload) : INotification
    where TEvent : IntegrationEvent;
