using Microsoft.EntityFrameworkCore;
using ScfPlatform.BuildingBlocks.Application;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IInboxStore"/> against a module's own
/// <typeparamref name="TDbContext"/> (Foundations §6.3). Registered per module, scoped to
/// that module's <c>DbContext</c>, once that module's own unit builds its persistence layer.
/// </summary>
public sealed class EfInboxStore<TDbContext> : IInboxStore
    where TDbContext : DbContext
{
    private readonly TDbContext _context;

    public EfInboxStore(TDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        _context.Set<InboxMessage>().AnyAsync(message => message.EventId == eventId, cancellationToken);

    public async Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        _context.Set<InboxMessage>().Add(new InboxMessage
        {
            EventId = eventId,
            ProcessedOnUtc = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
