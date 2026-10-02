using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// The single place the mediator is configured for the whole monolith
/// (Handover_Packages/00-Shared-Foundations.md §4 "one composition root"). The host calls
/// this once, passing every module's Application assembly, so there is exactly one MediatR
/// registration, one pipeline, and one place the <see cref="ValidationBehavior{TRequest,TResponse}"/>
/// is wired — no per-module duplication.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    public static IServiceCollection AddCogniJobsMediator(
        this IServiceCollection services,
        params Assembly[] moduleApplicationAssemblies)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblies(moduleApplicationAssemblies);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
