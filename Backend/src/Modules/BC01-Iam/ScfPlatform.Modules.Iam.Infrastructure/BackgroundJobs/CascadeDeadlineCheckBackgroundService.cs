using ScfPlatform.Modules.Iam.Application.InboundEvents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScfPlatform.Modules.Iam.Infrastructure.BackgroundJobs;

/// <summary>BC-01-IAM-and-UAM.md §8.3 — the scheduled trigger for <see cref="CheckCascadeDeadlinesCommand"/>, "same shape as BC-4's ProcessExpiredPostingsCommand" (§3).</summary>
public sealed class CascadeDeadlineCheckBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CascadeDeadlineCheckBackgroundService> _logger;

    public CascadeDeadlineCheckBackgroundService(IServiceScopeFactory scopeFactory, ILogger<CascadeDeadlineCheckBackgroundService> logger)
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
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new CheckCascadeDeadlinesCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AccountDeactivationCascade deadline check failed this cycle");
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
}
