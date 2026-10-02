using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.Modules.Iam.Infrastructure.Repositories;

public sealed class AdminActionLogRepository : IAdminActionLogRepository
{
    private readonly IamDbContext _context;

    public AdminActionLogRepository(IamDbContext context) => _context = context;

    public async Task AddAsync(AdminActionLog entry, CancellationToken cancellationToken) =>
        await _context.AdminActionLogs.AddAsync(entry, cancellationToken);

    public async Task<(IReadOnlyList<AdminActionLog> Items, int TotalCount)> QueryAsync(AdminActionLogQuery query, CancellationToken cancellationToken)
    {
        var entries = _context.AdminActionLogs.AsQueryable();

        if (query.AdminUserId is not null)
        {
            entries = entries.Where(e => e.AdminUserId == query.AdminUserId);
        }

        if (query.TargetUserId is not null)
        {
            entries = entries.Where(e => e.TargetUserId == query.TargetUserId);
        }

        if (query.ActionType is not null)
        {
            entries = entries.Where(e => e.ActionType == query.ActionType);
        }

        if (query.FromUtc is not null)
        {
            entries = entries.Where(e => e.OccurredOnUtc >= query.FromUtc);
        }

        if (query.ToUtc is not null)
        {
            entries = entries.Where(e => e.OccurredOnUtc <= query.ToUtc);
        }

        var totalCount = await entries.CountAsync(cancellationToken);

        var items = await entries
            .OrderByDescending(e => e.OccurredOnUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
