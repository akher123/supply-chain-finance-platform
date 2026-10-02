using ScfPlatform.BuildingBlocks.Infrastructure.Outbox;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Contracts;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScfPlatform.Modules.Iam.Infrastructure.BackgroundJobs;

/// <summary>BC-01-IAM-and-UAM.md §3 — "a session-expiry sweep that marks sessions past their inactivity/absolute expiry" (<see cref="UserAccount.ExpireStaleSessions"/>).</summary>
public sealed class SessionExpirySweepBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionExpirySweepBackgroundService> _logger;

    public SessionExpirySweepBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SessionExpirySweepBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Session-expiry sweep failed this cycle");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var now = DateTime.UtcNow;

        var accountsWithActiveSessions = await context.UserAccounts
            .Where(a => a.Sessions.Any(s => s.RevokedOnUtc == null))
            .ToListAsync(cancellationToken);

        var anyExpired = false;

        foreach (var account in accountsWithActiveSessions)
        {
            var expired = account.ExpireStaleSessions(now, UserAccount.DefaultSessionInactivityWindow);

            foreach (var sessionId in expired)
            {
                anyExpired = true;
                OutboxWriter.Enqueue(context, new UserLoggedOutIntegrationEvent(account.Id.Value, sessionId.Value, "idle-timeout", now));
            }
        }

        if (anyExpired)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
