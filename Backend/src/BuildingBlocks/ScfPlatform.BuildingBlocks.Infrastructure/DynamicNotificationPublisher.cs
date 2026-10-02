using MediatR;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.BuildingBlocks.Domain;
using System.Collections.Concurrent;

namespace ScfPlatform.BuildingBlocks.Infrastructure;

/// <summary>
/// Publishes a <see cref="DomainEvent"/> or <see cref="IntegrationEvent"/> whose concrete
/// type is only known at runtime (from EF's <c>ChangeTracker</c>, or deserialized off the
/// outbox) by constructing the matching closed <see cref="DomainEventNotification{TDomainEvent}"/>
/// / <see cref="IntegrationEventNotification{TEvent}"/> via reflection and handing it to
/// MediatR's <see cref="IPublisher"/>. Both dispatch paths (§6.1 domain events, §6.2/§6.6
/// integration events) funnel through here so there is exactly one place doing the
/// reflection trick MediatR's generic notifications require for a dynamic payload type.
/// </summary>
public static class DynamicNotificationPublisher
{
    private static readonly ConcurrentDictionary<Type, Type> DomainNotificationTypes = new();
    private static readonly ConcurrentDictionary<Type, Type> IntegrationNotificationTypes = new();

    public static Task PublishDomainEventAsync(IPublisher publisher, DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var notificationType = DomainNotificationTypes.GetOrAdd(
            domainEvent.GetType(),
            eventType => typeof(DomainEventNotification<>).MakeGenericType(eventType));

        var notification = Activator.CreateInstance(notificationType, domainEvent)!;

        return publisher.Publish(notification, cancellationToken);
    }

    public static Task PublishIntegrationEventAsync(IPublisher publisher, IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var notificationType = IntegrationNotificationTypes.GetOrAdd(
            integrationEvent.GetType(),
            eventType => typeof(IntegrationEventNotification<>).MakeGenericType(eventType));

        var notification = Activator.CreateInstance(notificationType, integrationEvent)!;

        return publisher.Publish(notification, cancellationToken);
    }
}
