using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.Modules.Iam.Application.Abstractions;

/// <summary>
/// This module's single unit of work (00-Shared-Foundations.md §6.4 "one persistence context /
/// unit-of-work per module"). <see cref="EnqueueIntegrationEvent{TEvent}"/> stages an integration
/// event onto the outbox on the *same* underlying <c>DbContext</c> instance that the
/// repositories in this scope use — calling it and then <see cref="SaveChangesAsync"/> writes the
/// aggregate change and the outbox row in one transaction (§6.2), the same pattern the B0 outbox
/// test harness uses (enqueue-then-save, not post-commit domain-event dispatch — see this
/// module's progress ledger for the write-up on why).
/// </summary>
public interface IIamUnitOfWork
{
    void EnqueueIntegrationEvent<TEvent>(TEvent integrationEvent) where TEvent : IntegrationEvent;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
