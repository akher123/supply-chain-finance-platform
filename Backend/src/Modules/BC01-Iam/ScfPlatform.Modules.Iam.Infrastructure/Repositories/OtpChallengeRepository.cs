using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ScfPlatform.Modules.Iam.Infrastructure.Repositories;

public sealed class OtpChallengeRepository : IOtpChallengeRepository
{
    private readonly IamDbContext _context;

    public OtpChallengeRepository(IamDbContext context) => _context = context;

    public Task<OtpChallenge?> GetByIdAsync(OtpChallengeId id, CancellationToken cancellationToken) =>
        _context.OtpChallenges.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<OtpChallenge?> GetActiveByAccountAndPurposeAsync(UserAccountId accountId, OtpPurpose purpose, CancellationToken cancellationToken) =>
        _context.OtpChallenges
            .Where(c => c.UserAccountId == accountId && c.Purpose == purpose && c.Status == OtpStatus.Issued)
            .OrderByDescending(c => c.IssuedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken) =>
        await _context.OtpChallenges.AddAsync(challenge, cancellationToken);

    public void Update(OtpChallenge challenge) => _context.OtpChallenges.Update(challenge);
}
