using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Contracts;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.InboundEvents;

/// <summary>
/// BC-01-IAM-and-UAM.md §8.3 — the AccountDeactivationCascade saga's timeout signal. Run on a
/// schedule (Infrastructure, same shape as BC-4's <c>ProcessExpiredPostingsCommand</c>, §3):
/// marks any run past its 24h deadline still <c>InProgress</c> as <c>Incomplete</c> and raises
/// <see cref="DeactivationCascadeIncompleteIntegrationEvent"/>.
/// </summary>
public sealed record CheckCascadeDeadlinesCommand : IRequest<Result>;

public sealed class CheckCascadeDeadlinesCommandHandler : IRequestHandler<CheckCascadeDeadlinesCommand, Result>
{
    private readonly IDeactivationCascadeRunRepository _cascadeRuns;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CheckCascadeDeadlinesCommandHandler(IDeactivationCascadeRunRepository cascadeRuns, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _cascadeRuns = cascadeRuns;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(CheckCascadeDeadlinesCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var inProgressRuns = await _cascadeRuns.GetInProgressAsync(cancellationToken);
        var anyFlipped = false;

        foreach (var run in inProgressRuns)
        {
            if (!run.MarkIncompleteIfPastDeadline(nowUtc))
            {
                continue;
            }

            anyFlipped = true;
            _cascadeRuns.Update(run);
            _unitOfWork.EnqueueIntegrationEvent(new DeactivationCascadeIncompleteIntegrationEvent(run.UserId.Value, run.MissingSignals));
        }

        if (anyFlipped)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
