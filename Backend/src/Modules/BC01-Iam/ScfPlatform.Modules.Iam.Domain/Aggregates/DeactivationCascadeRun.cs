using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Domain.Aggregates;

/// <summary>The <c>DeactivationCascadeRun</c>'s own lifecycle (BC-01 §8.3).</summary>
public enum DeactivationCascadeStatus
{
    InProgress,
    Completed,
    Incomplete,
}

/// <summary>
/// BC-01-IAM-and-UAM.md §8.3. A small BC-1-internal aggregate tracking the
/// <c>AccountDeactivationCascade</c> saga — <b>not</b> exposed via <c>Contracts</c>. BC-1 owns
/// this as an in-module process manager rather than a 13th bounded context: BC-2/3/4/5 already
/// react to <c>AccountDeactivated</c> independently and idempotently (unchanged by this
/// aggregate) — the only value this run adds is observable completion and a timeout alert.
/// </summary>
public sealed class DeactivationCascadeRun : AggregateRoot<DeactivationCascadeRunId>
{
    public static readonly TimeSpan Deadline = TimeSpan.FromHours(24);

    /// <summary>The fixed signal vocabulary this run correlates against (§8.3): BC-4's <c>JobPostingClosedIntegrationEvent</c> (<c>Reason = AccountDeactivated</c>) and BC-5's <c>ApplicationWithdrawnIntegrationEvent</c> (<c>WithdrawalReason = AccountDeactivated</c>).</summary>
    public const string JobPostingClosedSignal = "JobPostingClosed";

    public const string ApplicationWithdrawnSignal = "ApplicationWithdrawn";

    private readonly List<string> _receivedSignals = [];

    // ORM materialization only.
    private DeactivationCascadeRun()
    {
    }

    private DeactivationCascadeRun(DeactivationCascadeRunId id, UserAccountId userId, DateTime startedOnUtc, IEnumerable<string> autoSatisfiedSignals)
        : base(id)
    {
        UserId = userId;
        StartedOnUtc = startedOnUtc;
        DeadlineUtc = startedOnUtc.Add(Deadline);
        Status = DeactivationCascadeStatus.InProgress;
        _receivedSignals.AddRange(autoSatisfiedSignals);

        if (IsComplete)
        {
            Status = DeactivationCascadeStatus.Completed;
        }
    }

    public UserAccountId UserId { get; private set; } = null!;

    public DateTime StartedOnUtc { get; private set; }

    public DateTime DeadlineUtc { get; private set; }

    public DeactivationCascadeStatus Status { get; private set; }

    public IReadOnlyList<string> ExpectedSignals { get; } = [JobPostingClosedSignal, ApplicationWithdrawnSignal];

    public IReadOnlyList<string> ReceivedSignals => _receivedSignals.AsReadOnly();

    public IReadOnlyList<string> MissingSignals => ExpectedSignals.Except(_receivedSignals, StringComparer.Ordinal).ToList();

    private bool IsComplete => ExpectedSignals.All(_receivedSignals.Contains);

    /// <summary>
    /// Starts a run for a just-deactivated account. The signal inapplicable to
    /// <paramref name="role"/> is auto-satisfied (§8.3): an employer account never produces
    /// <see cref="ApplicationWithdrawnSignal"/>, a job-seeker account never produces
    /// <see cref="JobPostingClosedSignal"/>; any other role auto-satisfies both (the run
    /// completes immediately).
    /// </summary>
    public static DeactivationCascadeRun Start(UserAccountId userId, UserRole role, DateTime startedOnUtc)
    {
        var autoSatisfied = role switch
        {
            UserRole.Employer => new[] { ApplicationWithdrawnSignal },
            UserRole.JobSeeker => new[] { JobPostingClosedSignal },
            _ => new[] { JobPostingClosedSignal, ApplicationWithdrawnSignal },
        };

        return new DeactivationCascadeRun(DeactivationCascadeRunId.New(), userId, startedOnUtc, autoSatisfied);
    }

    /// <summary>Correlates an inbound cascade signal (§9.1) to this run. Idempotent — receiving the same signal twice is a no-op.</summary>
    public void RecordSignalReceived(string signal)
    {
        if (Status != DeactivationCascadeStatus.InProgress)
        {
            return;
        }

        if (!_receivedSignals.Contains(signal, StringComparer.Ordinal))
        {
            _receivedSignals.Add(signal);
        }

        if (IsComplete)
        {
            Status = DeactivationCascadeStatus.Completed;
        }
    }

    /// <summary>Invoked by the scheduled <c>CheckCascadeDeadlinesCommand</c> (§8.3). Returns true (and flips to <see cref="DeactivationCascadeStatus.Incomplete"/>) only the first time it is called past the deadline while still in progress.</summary>
    public bool MarkIncompleteIfPastDeadline(DateTime nowUtc)
    {
        if (Status != DeactivationCascadeStatus.InProgress || nowUtc <= DeadlineUtc)
        {
            return false;
        }

        Status = DeactivationCascadeStatus.Incomplete;
        return true;
    }
}
