using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.Modules.Iam.Infrastructure.Repositories;

/// <summary>BC-01-IAM-and-UAM.md §11.3. Owned collections (Sessions/TrustedDevices/BackupCodes/PasswordResetTokens) load automatically with the aggregate — EF Core owned types are always included.</summary>
public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly IamDbContext _context;

    public UserAccountRepository(IamDbContext context) => _context = context;

    public Task<UserAccount?> GetByIdAsync(UserAccountId id, CancellationToken cancellationToken) =>
        _context.UserAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        _context.UserAccounts.FirstOrDefaultAsync(a => a.Credential.Email.Value == email, cancellationToken);

    public Task<UserAccount?> GetByMobileAsync(string mobile, CancellationToken cancellationToken) =>
        _context.UserAccounts.FirstOrDefaultAsync(a => a.Credential.Mobile.Value == mobile, cancellationToken);

    public Task<UserAccount?> GetByEmailOrMobileAsync(string identifier, CancellationToken cancellationToken) =>
        _context.UserAccounts.FirstOrDefaultAsync(
            a => a.Credential.Email.Value == identifier || a.Credential.Mobile.Value == identifier, cancellationToken);

    public Task<UserAccount?> GetBySessionRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken) =>
        _context.UserAccounts.FirstOrDefaultAsync(a => a.Sessions.Any(s => s.RefreshTokenHash == refreshTokenHash), cancellationToken);

    public Task<UserAccount?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        _context.UserAccounts.FirstOrDefaultAsync(a => a.PasswordResetTokens.Any(t => t.TokenHash == tokenHash), cancellationToken);

    public async Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> SearchAsync(UserSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = _context.UserAccounts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var text = criteria.SearchText.Trim().ToLowerInvariant();
            query = query.Where(a => a.Credential.Email.Value.Contains(text));
        }

        if (criteria.Role is not null)
        {
            query = query.Where(a => a.Role == criteria.Role);
        }

        if (criteria.Status is not null)
        {
            query = query.Where(a => a.Status == criteria.Status);
        }

        if (criteria.RegisteredFromUtc is not null)
        {
            query = query.Where(a => a.CreatedOnUtc >= criteria.RegisteredFromUtc);
        }

        if (criteria.RegisteredToUtc is not null)
        {
            query = query.Where(a => a.CreatedOnUtc <= criteria.RegisteredToUtc);
        }

        if (criteria.IdentityVerified is not null)
        {
            query = query.Where(a => a.IdentityVerified == criteria.IdentityVerified);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedOnUtc)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken) =>
        await _context.UserAccounts.AddAsync(account, cancellationToken);

    public void Update(UserAccount account) => _context.UserAccounts.Update(account);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        _context.UserAccounts.AnyAsync(a => a.Credential.Email.Value == email, cancellationToken);

    public Task<bool> MobileExistsAsync(string mobile, CancellationToken cancellationToken) =>
        _context.UserAccounts.AnyAsync(a => a.Credential.Mobile.Value == mobile, cancellationToken);
}
