namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Port over the platform's single shared real-time channel (Foundations §6.6). A module's
/// Application-layer integration-event handler calls this — typically from an
/// <see cref="IdempotentIntegrationEventHandler{TEvent}"/> subclass — to fan an
/// already-committed, already-authorized event out to connected clients. This is never a
/// new source of truth; it only republishes what the outbox already delivered in-process.
/// Implemented in Infrastructure against the shared SignalR hub.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Pushes to every connection authenticated as this user.</summary>
    Task PushToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken);

    /// <summary>Pushes to every connection whose user holds this role (e.g. "SystemAdministrator", "IntegrationEngineer").</summary>
    Task PushToRoleAsync(string role, string eventName, object payload, CancellationToken cancellationToken);
}
