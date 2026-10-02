using ScfPlatform.BuildingBlocks.Application;
using Microsoft.AspNetCore.SignalR;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Realtime;

/// <summary>SignalR-backed implementation of <see cref="IRealtimeNotifier"/> (Foundations §6.6).</summary>
public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<CogniJobsHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<CogniJobsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PushToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(HubGroups.ForUser(userId)).SendAsync(eventName, payload, cancellationToken);

    public Task PushToRoleAsync(string role, string eventName, object payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(HubGroups.ForRole(role)).SendAsync(eventName, payload, cancellationToken);
}
