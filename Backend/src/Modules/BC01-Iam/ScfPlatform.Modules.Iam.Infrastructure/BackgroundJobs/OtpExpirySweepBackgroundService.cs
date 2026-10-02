using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScfPlatform.Modules.Iam.Infrastructure.BackgroundJobs;

/// <summary>BC-01-IAM-and-UAM.md §3 — "an OTP-expiry sweep that moves stale Issued OtpChallenges to Expired."</summary>
public sealed class OtpExpirySweepBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OtpExpirySweepBackgroundService> _logger;

    public OtpExpirySweepBackgroundService(IServiceScopeFactory scopeFactory, ILogger<OtpExpirySweepBackgroundService> logger)
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
                _logger.LogError(ex, "OTP-expiry sweep failed this cycle");
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

        var stale = await context.OtpChallenges
            .Where(c => c.Status == OtpStatus.Issued && c.ExpiresOnUtc < now)
            .ToListAsync(cancellationToken);

        foreach (var challenge in stale)
        {
            challenge.MarkExpired();
        }

        if (stale.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
