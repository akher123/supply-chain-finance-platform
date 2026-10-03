using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.4-01 AC-07 — admin soft-deletes an account and starts the §8.3 AccountDeactivationCascade tracking run.</summary>
public sealed record AdminDeactivateUserCommand(Guid AdminUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class AdminDeactivateUserCommandValidator : AbstractValidator<AdminDeactivateUserCommand>
{
    public AdminDeactivateUserCommandValidator()
    {
        RuleFor(c => c.AdminUserId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
    }
}

public sealed class AdminDeactivateUserCommandHandler : IRequestHandler<AdminDeactivateUserCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IDeactivationCascadeRunRepository _cascadeRuns;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AdminDeactivateUserCommandHandler(
        IUserAccountRepository userAccounts,
        IAdminActionLogRepository adminActionLogs,
        IDeactivationCascadeRunRepository cascadeRuns,
        IIamUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _cascadeRuns = cascadeRuns;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AdminDeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var loadResult = await AdminActionSupport.LoadTargetAsync(_userAccounts, request.TargetUserId, cancellationToken);

        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        var account = loadResult.Value;
        var deactivateResult = account.Deactivate(nowUtc);

        if (deactivateResult.IsFailure)
        {
            return deactivateResult;
        }

        _userAccounts.Update(account);
        await AdminActionSupport.RecordAsync(_adminActionLogs, request.AdminUserId, AdminActionType.Deactivated, request.TargetUserId, null, nowUtc, cancellationToken);

        var cascadeRun = DeactivationCascadeRun.Start(account.Id, account.Role, nowUtc);
        await _cascadeRuns.AddAsync(cascadeRun, cancellationToken);

        _unitOfWork.EnqueueIntegrationEvent(new AccountDeactivatedIntegrationEvent(account.Id.Value, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
