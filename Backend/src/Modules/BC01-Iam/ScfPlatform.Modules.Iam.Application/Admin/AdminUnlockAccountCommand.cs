using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.4-01 AC-10 — admin unlocks a locked account. No integration event (Unlock is not in §8.1's published list).</summary>
public sealed record AdminUnlockAccountCommand(Guid AdminUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class AdminUnlockAccountCommandValidator : AbstractValidator<AdminUnlockAccountCommand>
{
    public AdminUnlockAccountCommandValidator()
    {
        RuleFor(c => c.AdminUserId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
    }
}

public sealed class AdminUnlockAccountCommandHandler : IRequestHandler<AdminUnlockAccountCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AdminUnlockAccountCommandHandler(
        IUserAccountRepository userAccounts, IAdminActionLogRepository adminActionLogs, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AdminUnlockAccountCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var loadResult = await AdminActionSupport.LoadTargetAsync(_userAccounts, request.TargetUserId, cancellationToken);

        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        var account = loadResult.Value;
        account.Unlock(nowUtc);

        _userAccounts.Update(account);
        await AdminActionSupport.RecordAsync(_adminActionLogs, request.AdminUserId, AdminActionType.Unlocked, request.TargetUserId, null, nowUtc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
