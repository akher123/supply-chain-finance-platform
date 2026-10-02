using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.Modules.Iam.Infrastructure.Repositories;

public sealed class DeactivationCascadeRunRepository : IDeactivationCascadeRunRepository
{
    private readonly IamDbContext _context;

    public DeactivationCascadeRunRepository(IamDbContext context) => _context = context;

    public Task<DeactivationCascadeRun?> GetByUserIdAsync(UserAccountId userId, CancellationToken cancellationToken) =>
        _context.DeactivationCascadeRuns
            .Where(r => r.UserId == userId && r.Status == DeactivationCascadeStatus.InProgress)
            .OrderByDescending(r => r.StartedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DeactivationCascadeRun>> GetInProgressAsync(CancellationToken cancellationToken) =>
        await _context.DeactivationCascadeRuns
            .Where(r => r.Status == DeactivationCascadeStatus.InProgress)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(DeactivationCascadeRun run, CancellationToken cancellationToken) =>
        await _context.DeactivationCascadeRuns.AddAsync(run, cancellationToken);

    public void Update(DeactivationCascadeRun run) => _context.DeactivationCascadeRuns.Update(run);
}
