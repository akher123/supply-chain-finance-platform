namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Port over a module's <c>inbox_messages</c> table (Foundations §6.3/§6.5). Implemented in
/// Infrastructure against that module's own <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public interface IInboxStore
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken);
}
