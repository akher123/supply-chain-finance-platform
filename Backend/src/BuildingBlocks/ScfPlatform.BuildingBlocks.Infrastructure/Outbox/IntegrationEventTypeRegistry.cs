using ScfPlatform.BuildingBlocks.Domain;
using System.Collections.Concurrent;
using System.Reflection;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Resolves an <see cref="OutboxMessage.Type"/> string back to its CLR <see cref="Type"/> so
/// the outbox relay can deserialize the stored JSON payload back into the concrete
/// integration event before publishing it in-process. Populated once at startup by scanning
/// every module's frozen <c>Contracts</c> assembly — the only assembly that defines
/// concrete <see cref="IntegrationEvent"/> types.
/// </summary>
public sealed class IntegrationEventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _typesByName = new();

    public IntegrationEventTypeRegistry RegisterAssembly(Assembly contractsAssembly)
    {
        var eventTypes = contractsAssembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(IntegrationEvent).IsAssignableFrom(type));

        foreach (var eventType in eventTypes)
        {
            _typesByName[eventType.FullName ?? eventType.Name] = eventType;
        }

        return this;
    }

    public IntegrationEventTypeRegistry RegisterAssemblies(IEnumerable<Assembly> contractsAssemblies)
    {
        foreach (var assembly in contractsAssemblies)
        {
            RegisterAssembly(assembly);
        }

        return this;
    }

    public bool TryResolve(string typeName, out Type? eventType) => _typesByName.TryGetValue(typeName, out eventType);
}
