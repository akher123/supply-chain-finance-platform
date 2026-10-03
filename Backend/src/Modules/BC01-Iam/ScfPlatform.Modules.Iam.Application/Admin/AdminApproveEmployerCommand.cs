using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.4-01 AC-05 — admin approves a pending employer registration.</summary>
public sealed record AdminApproveEmployerCommand(Guid AdminUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class AdminApproveEmployerCommandValidator : AbstractValidator<AdminApproveEmployerCommand>
{
    public AdminApproveEmployerCommandValidator()
    {
        RuleFor(c => c.AdminUserId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
    }
}

public sealed class AdminApproveEmployerCommandHandler : IRequestHandler<AdminApproveEmployerCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AdminApproveEmployerCommandHandler(
        IUserAccountRepository userAccounts, IAdminActionLogRepository adminActionLogs, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AdminApproveEmployerCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var loadResult = await AdminActionSupport.LoadTargetAsync(_userAccounts, request.TargetUserId, cancellationToken);

        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        var account = loadResult.Value;
        var activateResult = account.Activate(nowUtc);

        if (activateResult.IsFailure)
        {
            return activateResult;
        }

        _userAccounts.Update(account);
        await AdminActionSupport.RecordAsync(_adminActionLogs, request.AdminUserId, AdminActionType.ApprovedEmployer, request.TargetUserId, null, nowUtc, cancellationToken);
        _unitOfWork.EnqueueIntegrationEvent(new UserAccountActivatedIntegrationEvent(account.Id.Value, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
