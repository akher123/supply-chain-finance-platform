using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Realtime;

/// <summary>
/// The platform's single, shared real-time hub (Foundations §6.6) — hosted once, in the
/// composition root, never per-module. On connect, a client is added to its user group and
/// every role group its claims carry, so it only ever receives what it's authorized to see.
/// Modules never touch this class directly — they go through <see cref="Application.IRealtimeNotifier"/>.
///
/// B0 scope: group membership is driven by whatever claims are already on <see cref="HubCallerContext.User"/>
/// at connect time. Real authentication (issuing/validating those claims) is BC-1's own
/// later unit — this hub works unauthenticated (no groups joined) until that lands, so it
/// composes and boots today without hard-depending on unbuilt IAM internals.
/// </summary>
public sealed class CogniJobsHub : Hub
{
    public const string RouteTemplate = "/hubs/realtime";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId(Context.User);
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ForUser(userId.Value));
        }

        foreach (var role in GetRoles(Context.User))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.ForRole(role));
        }

        await base.OnConnectedAsync();
    }

    private static Guid? GetUserId(ClaimsPrincipal? user)
    {
        var value = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value;

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IEnumerable<string> GetRoles(ClaimsPrincipal? user) =>
        user?.FindAll(ClaimTypes.Role).Select(claim => claim.Value) ?? [];
}
