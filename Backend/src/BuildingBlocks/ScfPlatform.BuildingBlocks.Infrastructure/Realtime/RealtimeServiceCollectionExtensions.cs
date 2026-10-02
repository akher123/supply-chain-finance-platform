using ScfPlatform.BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Realtime;

/// <summary>
/// Registers the shared SignalR hub once, from the host composition root (Foundations §6.6).
/// </summary>
public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddCogniJobsRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

        return services;
    }

    public static IEndpointRouteBuilder MapCogniJobsRealtimeHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<CogniJobsHub>(CogniJobsHub.RouteTemplate);

        return endpoints;
    }
}
