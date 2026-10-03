using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Contracts;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>Shared "revoke one session, blacklist its refresh token, enqueue UserLoggedOut" tail used by <c>RevokeTokenCommand</c> and <c>LogoutCommand</c> (BC-01-IAM-and-UAM.md §10.1).</summary>
public static class SessionRevocationSupport
{
    public static async Task<Result> RevokeOneAsync(
        UserAccount account,
        SessionId sessionId,
        string reason,
        DateTime nowUtc,
        IIamUnitOfWork unitOfWork,
        IRevokedTokenStore revokedTokens,
        CancellationToken cancellationToken)
    {
        var session = account.Sessions.FirstOrDefault(s => s.Id == sessionId);
        var revokeResult = account.RevokeSession(sessionId, nowUtc, reason);

        if (revokeResult.IsFailure)
        {
            return revokeResult;
        }

        if (session is not null)
        {
            await revokedTokens.AddAsync(session.RefreshTokenHash, nowUtc, session.ExpiresOnUtc, cancellationToken);
        }

        unitOfWork.EnqueueIntegrationEvent(new UserLoggedOutIntegrationEvent(account.Id.Value, sessionId.Value, reason, nowUtc));

        return Result.Success();
    }

    public static async Task RevokeAllAsync(
        UserAccount account,
        string reason,
        DateTime nowUtc,
        IIamUnitOfWork unitOfWork,
        IRevokedTokenStore revokedTokens,
        CancellationToken cancellationToken)
    {
        var sessionsBeforeRevocation = account.Sessions.Where(s => s.IsActive).ToDictionary(s => s.Id, s => s);
        var revokedIds = account.RevokeAllSessions(nowUtc, reason);

        foreach (var sessionId in revokedIds)
        {
            if (sessionsBeforeRevocation.TryGetValue(sessionId, out var session))
            {
                await revokedTokens.AddAsync(session.RefreshTokenHash, nowUtc, session.ExpiresOnUtc, cancellationToken);
            }

            unitOfWork.EnqueueIntegrationEvent(new UserLoggedOutIntegrationEvent(account.Id.Value, sessionId.Value, reason, nowUtc));
        }
    }
}
