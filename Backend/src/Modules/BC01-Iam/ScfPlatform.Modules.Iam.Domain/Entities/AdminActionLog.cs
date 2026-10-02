using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;

namespace ScfPlatform.Modules.Iam.Domain.Entities;

/// <summary>
/// BC-01-IAM-and-UAM.md §5.3, §6.4. A thin append-only log, persisted directly — not a
/// behavioural aggregate. One factory (<see cref="Record"/>) and no mutators; never updated or
/// deleted. The application handler for every admin command appends one row in the *same* unit
/// of work as the <c>UserAccount</c> change (§6.2 invariant #11).
/// </summary>
public sealed class AdminActionLog : Entity<Guid>
{
    // ORM materialization only.
    private AdminActionLog()
    {
    }

    private AdminActionLog(Guid id, Guid adminUserId, AdminActionType actionType, Guid targetUserId, string? reason, DateTime occurredOnUtc)
        : base(id)
    {
        AdminUserId = adminUserId;
        ActionType = actionType;
        TargetUserId = targetUserId;
        Reason = reason;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid AdminUserId { get; private set; }

    public AdminActionType ActionType { get; private set; }

    public Guid TargetUserId { get; private set; }

    public string? Reason { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    public static AdminActionLog Record(Guid adminUserId, AdminActionType actionType, Guid targetUserId, string? reason, DateTime nowUtc) =>
        new(Guid.NewGuid(), adminUserId, actionType, targetUserId, reason, nowUtc);
}
