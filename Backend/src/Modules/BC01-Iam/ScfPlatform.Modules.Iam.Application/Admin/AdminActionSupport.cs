using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Aggregates;
using ScfPlatform.Modules.Iam.Domain.Entities;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>
/// Shared "load target, append AdminActionLog row in the same unit of work" support for every
/// admin command cluster handler (BC-01-IAM-and-UAM.md §10.1, §6.2 invariant #11). Authorization
/// (caller must be an admin) is enforced at the API layer's route gate before dispatch — every
/// command here still records <c>AdminUserId</c> for the audit trail.
/// </summary>
public static class AdminActionSupport
{
    public static async Task<Result<UserAccount>> LoadTargetAsync(IUserAccountRepository userAccounts, Guid targetUserId, CancellationToken cancellationToken)
    {
        var account = await userAccounts.GetByIdAsync(new UserAccountId(targetUserId), cancellationToken);

        return account is null
            ? Result.Failure<UserAccount>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."))
            : Result.Success(account);
    }

    public static Task RecordAsync(
        IAdminActionLogRepository adminActionLogs,
        Guid adminUserId,
        AdminActionType actionType,
        Guid targetUserId,
        string? reason,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        adminActionLogs.AddAsync(AdminActionLog.Record(adminUserId, actionType, targetUserId, reason, nowUtc), cancellationToken);
}
