using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScfPlatform.Modules.Iam.Infrastructure.BackgroundJobs;

/// <summary>BC-01-IAM-and-UAM.md §3 — "a revoked-token / expired-OTP cleanup job that deletes rows from revoked_tokens and otp_challenges once past their expiry, keeping those high-churn tables small" (§11.2).</summary>
public sealed class RevokedTokenAndOtpCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan OtpRetention = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevokedTokenAndOtpCleanupBackgroundService> _logger;

    public RevokedTokenAndOtpCleanupBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RevokedTokenAndOtpCleanupBackgroundService> logger)
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
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Revoked-token/OTP cleanup failed this cycle");
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

    public async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var now = DateTime.UtcNow;

        await context.RevokedTokens
            .Where(r => r.ExpiresOnUtc < now)
            .ExecuteDeleteAsync(cancellationToken);

        await context.OtpChallenges
            .Where(c => c.Status != OtpStatus.Issued && c.ExpiresOnUtc < now.Subtract(OtpRetention))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
