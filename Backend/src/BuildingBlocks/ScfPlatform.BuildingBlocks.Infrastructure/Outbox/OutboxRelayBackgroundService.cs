using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScfPlatform.BuildingBlocks.Domain;
using System.Text.Json;

namespace ScfPlatform.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// The background relay for one module's outbox (Foundations §6.2/§6.6): polls
/// <c>outbox_messages</c> for unprocessed rows, resolves + deserializes each back to its
/// concrete <see cref="IntegrationEvent"/> via <see cref="IntegrationEventTypeRegistry"/>,
/// publishes it in-process (fan-out target: other modules' inbox handlers, and — for
/// modules that push live updates — the shared SignalR hub, §6.6), then stamps
/// <see cref="OutboxMessage.ProcessedOnUtc"/>. Never a new source of truth: it only
/// republishes what already committed.
///
/// One instance is registered per module (generic over that module's <c>DbContext</c>) from
/// the host composition root, e.g.
/// <code>services.AddHostedService(sp => new OutboxRelayBackgroundService&lt;IamDbContext&gt;(sp.GetRequiredService&lt;IServiceScopeFactory&gt;(), registry, logger));</code>
/// </summary>
public sealed class OutboxRelayBackgroundService<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IntegrationEventTypeRegistry _typeRegistry;
    private readonly ILogger<OutboxRelayBackgroundService<TDbContext>> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public OutboxRelayBackgroundService(
        IServiceScopeFactory scopeFactory,
        IntegrationEventTypeRegistry typeRegistry,
        ILogger<OutboxRelayBackgroundService<TDbContext>> logger,
        TimeSpan? pollInterval = null,
        int batchSize = 20)
    {
        _scopeFactory = scopeFactory;
        _typeRegistry = typeRegistry;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        _batchSize = batchSize;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox relay for {DbContext} failed this cycle", typeof(TDbContext).Name);
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
    }

    /// <summary>Processes one batch of unprocessed outbox rows. Exposed so integration tests can drive the relay deterministically instead of waiting on the poll timer.</summary>
    public async Task RelayOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var pending = await context.Set<OutboxMessage>()
            .Where(message => message.ProcessedOnUtc == null)
            .OrderBy(message => message.OccurredOnUtc)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            if (!_typeRegistry.TryResolve(message.Type, out var eventType) || eventType is null)
            {
                message.Error = $"Unknown integration event type '{message.Type}'.";
                continue;
            }

            try
            {
                var integrationEvent = (IntegrationEvent)JsonSerializer.Deserialize(message.Content, eventType, SerializerOptions)!;

                await DynamicNotificationPublisher.PublishIntegrationEventAsync(publisher, integrationEvent, cancellationToken);

                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Error = ex.Message;
                _logger.LogError(ex, "Failed to relay outbox message {MessageId} ({Type})", message.Id, message.Type);
            }
        }

        if (pending.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
