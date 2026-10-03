using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.4-01 / US-3.1.5-04 — admin-only role assignment. The "only an MoLAdministrator may be the grantor" invariant (§6.1) is enforced by the API layer's route gate (both this route and every other <c>/admin/*</c> route require <c>users:manage</c>, which only <c>MoLAdministrator</c> carries).</summary>
public sealed record AssignRoleCommand(Guid AdminUserId, Guid TargetUserId, UserRole Role) : IRequest<Result>;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(c => c.AdminUserId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
        RuleFor(c => c.Role).IsInEnum();
    }
}

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AssignRoleCommandHandler(
        IUserAccountRepository userAccounts, IAdminActionLogRepository adminActionLogs, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var loadResult = await AdminActionSupport.LoadTargetAsync(_userAccounts, request.TargetUserId, cancellationToken);

        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        var account = loadResult.Value;
        account.AssignRole(request.Role, nowUtc);

        _userAccounts.Update(account);
        await AdminActionSupport.RecordAsync(_adminActionLogs, request.AdminUserId, AdminActionType.RoleAssigned, request.TargetUserId, null, nowUtc, cancellationToken);
        _unitOfWork.EnqueueIntegrationEvent(new RoleAssignedIntegrationEvent(account.Id.Value, request.Role.ToString(), request.AdminUserId, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
