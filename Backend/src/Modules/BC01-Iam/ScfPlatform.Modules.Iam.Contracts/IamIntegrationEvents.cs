using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.Modules.Iam.Contracts;

// Frozen integration-event contract for BC-1 IAM and UAM.
// Source: Handover_Packages/BC-01-IAM-and-UAM.md §8.1, Stories/Event_Catalog.md.
// Shapes are the published language other modules subscribe to — additive changes only.

/// <summary>Raised when <c>Provision</c> succeeds. Consumed by BC-2, BC-3 (create profile shell), BC-9, BC-10, BC-12.</summary>
public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Role, string Email, DateTime CreatedAt) : IntegrationEvent;

/// <summary>Raised when <c>Activate</c> succeeds (OTP/email confirmation). Consumed by BC-2, BC-3, BC-9, BC-10.</summary>
public sealed record UserAccountActivatedIntegrationEvent(Guid UserId, DateTime ActivatedAt) : IntegrationEvent;

/// <summary>Raised when <c>Suspend</c> succeeds (admin moderation). Consumed by BC-2, BC-3, BC-4, BC-5, BC-9, BC-10.</summary>
public sealed record UserAccountSuspendedIntegrationEvent(Guid UserId, string Reason, Guid By, DateTime At) : IntegrationEvent;

/// <summary>Raised when <c>Reinstate</c> or <c>ReactivateAfterDeactivation</c> succeeds. <paramref name="By"/> is null for a self-reactivation. Consumed by BC-2, BC-3, BC-9, BC-10.</summary>
public sealed record UserAccountReinstatedIntegrationEvent(Guid UserId, Guid? By, DateTime At) : IntegrationEvent;

/// <summary>Raised when <c>Deactivate</c> succeeds (user self-deactivates). Consumed by BC-2, BC-3, BC-4, BC-5, BC-7, BC-9, BC-10; triggers the AccountDeactivationCascade (§8.3).</summary>
public sealed record AccountDeactivatedIntegrationEvent(Guid UserId, DateTime DeactivatedAt) : IntegrationEvent;

/// <summary>Raised on <c>RecordSuccessfulLogin</c>. Consumed by BC-10.</summary>
public sealed record UserLoggedInIntegrationEvent(Guid UserId, Guid SessionId, string Channel, DateTime At) : IntegrationEvent;

/// <summary>
/// Raised by <c>RevokeSession</c> / <c>RevokeAllSessions</c> / a <c>TouchSession</c> timeout-revocation.
/// <paramref name="Reason"/> is one of <c>"explicit"</c>, <c>"logout-all"</c>, <c>"idle-timeout"</c>, <c>"password-change"</c>.
/// Consumed by BC-10 (exact session tracking).
/// </summary>
public sealed record UserLoggedOutIntegrationEvent(Guid UserId, Guid SessionId, string Reason, DateTime At) : IntegrationEvent;

/// <summary>
/// Raised on <c>RecordFailedLogin</c>. <paramref name="Identifier"/> is the email/mobile the caller
/// attempted to log in with — never a <c>UserId</c>, since login may fail before identification.
/// Consumed by BC-10 (audit).
/// </summary>
public sealed record UserLoginFailedIntegrationEvent(string Identifier, string Reason, DateTime At) : IntegrationEvent;

/// <summary>Raised when <c>CompletePasswordReset</c> or <c>ChangePassword</c> succeeds. Consumed by BC-9, BC-10.</summary>
public sealed record PasswordResetIntegrationEvent(Guid UserId, DateTime At) : IntegrationEvent;

/// <summary>Raised on <c>AssignRole</c>. Consumed by BC-10.</summary>
public sealed record RoleAssignedIntegrationEvent(Guid UserId, string Role, Guid By, DateTime At) : IntegrationEvent;

/// <summary>
/// AccountDeactivationCascade saga's timeout signal (§8.3, new — not in the original
/// Event_Catalog.md table, added by BC-1's own resolved design). Raised by the scheduled
/// <c>CheckCascadeDeadlinesCommand</c> when a <c>DeactivationCascadeRun</c> is still
/// <c>InProgress</c> past its 24h deadline. Consumed by BC-9 (ops alert), BC-10 (audit).
/// </summary>
public sealed record DeactivationCascadeIncompleteIntegrationEvent(Guid UserId, IReadOnlyList<string> MissingSignals) : IntegrationEvent;

/// <summary>
/// Cross-package contract gap, resolved here: BC-09-Notification.md §9.1 documents consuming
/// this event and states plainly "BC-1's package (BC-01-IAM-and-UAM.md) publishes it" — the
/// clean seam is BC-1 generates/validates an OTP, BC-9 only delivers it — but BC-01's own
/// §8.1 table omitted it (an authoring oversight, not a design disagreement between the two
/// packages). Added here to make BC-1's Contracts match what BC-1's own package text already
/// promises BC-9. Raised whenever an OTP is issued (registration activation, password reset,
/// MFA challenge). BC-9 never generates or validates the code, only transports it.
/// </summary>
public sealed record OtpRequestedIntegrationEvent(Guid UserId, string Mobile, string Email, string OtpCode, string Purpose, DateTime ExpiresOnUtc) : IntegrationEvent;
