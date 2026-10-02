using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Registers every <see cref="IValidator{T}"/> found in the given module Application
/// assemblies. Called once from the host alongside <see cref="MediatorServiceCollectionExtensions.AddCogniJobsMediator"/>.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    public static IServiceCollection AddCogniJobsValidation(
        this IServiceCollection services,
        params Assembly[] moduleApplicationAssemblies)
    {
        services.AddValidatorsFromAssemblies(moduleApplicationAssemblies);

        return services;
    }
}
