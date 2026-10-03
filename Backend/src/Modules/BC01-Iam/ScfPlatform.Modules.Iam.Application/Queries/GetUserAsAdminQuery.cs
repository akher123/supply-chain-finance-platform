using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Queries;

/// <summary>BC-01-IAM-and-UAM.md §10.2 US-3.1.4-01 AC-04 — logs an <c>AdminActionLog(Viewed)</c> row (a query with a documented audit side effect).</summary>
public sealed record GetUserAsAdminQuery(Guid AdminUserId, Guid TargetUserId) : IRequest<Result<AdminUserDetailDto>>;

public sealed class GetUserAsAdminQueryValidator : AbstractValidator<GetUserAsAdminQuery>
{
    public GetUserAsAdminQueryValidator()
    {
        RuleFor(q => q.AdminUserId).NotEmpty();
        RuleFor(q => q.TargetUserId).NotEmpty();
    }
}

public sealed class GetUserAsAdminQueryHandler : IRequestHandler<GetUserAsAdminQuery, Result<AdminUserDetailDto>>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public GetUserAsAdminQueryHandler(
        IUserAccountRepository userAccounts, IAdminActionLogRepository adminActionLogs, IIamUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminUserDetailDto>> Handle(GetUserAsAdminQuery request, CancellationToken cancellationToken)
    {
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.TargetUserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure<AdminUserDetailDto>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _adminActionLogs.AddAsync(AdminActionLog.Record(request.AdminUserId, AdminActionType.Viewed, request.TargetUserId, null, nowUtc), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdminUserDetailDto(
            account.Id.Value,
            account.Credential.Email.Value,
            MobileMasking.Mask(account.Credential.Mobile.Value),
            account.Role,
            account.Status,
            account.CreatedOnUtc,
            account.IdentityVerified,
            account.LockState.IsLocked,
            account.LockState.LockedUntilUtc,
            account.Sessions.Count(s => s.IsActive)));
    }
}
