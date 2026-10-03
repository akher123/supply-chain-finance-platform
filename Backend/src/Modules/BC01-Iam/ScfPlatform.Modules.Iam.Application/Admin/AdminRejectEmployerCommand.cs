using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.4-01 AC-06 — admin rejects a pending employer registration (the reject path uses <c>Suspend</c>, §6.1).</summary>
public sealed record AdminRejectEmployerCommand(Guid AdminUserId, Guid TargetUserId, string Reason) : IRequest<Result>;

public sealed class AdminRejectEmployerCommandValidator : AbstractValidator<AdminRejectEmployerCommand>
{
    public AdminRejectEmployerCommandValidator()
    {
        RuleFor(c => c.AdminUserId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty();
    }
}

public sealed class AdminRejectEmployerCommandHandler : IRequestHandler<AdminRejectEmployerCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AdminRejectEmployerCommandHandler(
        IUserAccountRepository userAccounts, IAdminActionLogRepository adminActionLogs, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AdminRejectEmployerCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var loadResult = await AdminActionSupport.LoadTargetAsync(_userAccounts, request.TargetUserId, cancellationToken);

        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        var account = loadResult.Value;
        var suspendResult = account.Suspend(request.Reason, nowUtc);

        if (suspendResult.IsFailure)
        {
            return suspendResult;
        }

        _userAccounts.Update(account);
        await AdminActionSupport.RecordAsync(_adminActionLogs, request.AdminUserId, AdminActionType.RejectedEmployer, request.TargetUserId, request.Reason, nowUtc, cancellationToken);
        _unitOfWork.EnqueueIntegrationEvent(new UserAccountSuspendedIntegrationEvent(account.Id.Value, request.Reason, request.AdminUserId, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
