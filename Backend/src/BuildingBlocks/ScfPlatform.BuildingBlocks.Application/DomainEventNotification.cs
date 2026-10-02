
using MediatR;
using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Adapts a framework-free <see cref="DomainEvent"/> into a MediatR notification so the
/// dispatch pipeline (Foundations §6.1) can publish it in-process. The Domain layer never
/// references this type — only Infrastructure's save-changes interceptor does, after a
/// unit of work commits.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent Payload) : INotification
    where TDomainEvent : DomainEvent;
