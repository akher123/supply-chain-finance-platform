using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.BuildingBlocks.Infrastructure.Outbox;
using ScfPlatform.Modules.Iam.Application.Abstractions;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>
/// This module's <see cref="IIamUnitOfWork"/> implementation — wraps <see cref="IamDbContext"/>.
/// <see cref="EnqueueIntegrationEvent{TEvent}"/> stages a row via <see cref="OutboxWriter"/> on
/// this same tracked <see cref="DbContext"/>; <see cref="SaveChangesAsync"/> then commits the
/// aggregate changes and the outbox row together (see the ledger's "Key design decision").
/// </summary>
public sealed class IamUnitOfWork : IIamUnitOfWork
{
    private readonly IamDbContext _context;

    public IamUnitOfWork(IamDbContext context) => _context = context;

    public void EnqueueIntegrationEvent<TEvent>(TEvent integrationEvent) where TEvent : IntegrationEvent =>
        OutboxWriter.Enqueue(_context, integrationEvent);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
