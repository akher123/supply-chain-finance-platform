using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.Modules.Iam.Infrastructure.Repositories;

public sealed class RevokedTokenStore : IRevokedTokenStore
{
    private readonly IamDbContext _context;

    public RevokedTokenStore(IamDbContext context) => _context = context;

    public async Task AddAsync(string tokenIdOrRefreshHash, DateTime revokedOnUtc, DateTime expiresOnUtc, CancellationToken cancellationToken)
    {
        if (await _context.RevokedTokens.AnyAsync(r => r.TokenIdOrRefreshHash == tokenIdOrRefreshHash, cancellationToken))
        {
            return;
        }

        await _context.RevokedTokens.AddAsync(
            new RevokedTokenRecord { TokenIdOrRefreshHash = tokenIdOrRefreshHash, RevokedOnUtc = revokedOnUtc, ExpiresOnUtc = expiresOnUtc },
            cancellationToken);
    }

    public Task<bool> IsRevokedAsync(string tokenIdOrRefreshHash, CancellationToken cancellationToken) =>
        _context.RevokedTokens.AnyAsync(r => r.TokenIdOrRefreshHash == tokenIdOrRefreshHash, cancellationToken);
}
