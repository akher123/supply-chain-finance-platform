using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Domain.Events;

// Internal domain events (BC-01-IAM-and-UAM.md §8.2) raised by UserAccount — handled in-process
// within this module only; never published to the outbox. The corresponding *integration* events
// listed in §8.1 (UserLoggedOut, etc.) are constructed by the Application-layer command handler
// directly from the aggregate's post-call state, not raised as DomainEvents here — see the
// ledger's "Key design decision" note.

/// <summary>Raised by <c>RevokeSession</c>/<c>RevokeAllSessions</c>/a <c>TouchSession</c> timeout-revocation. <paramref name="Reason"/> mirrors the values this module's Contracts' <c>UserLoggedOutIntegrationEvent</c> uses ("explicit"/"logout-all"/"idle-timeout"/"password-change").</summary>
public sealed record SessionRevokedDomainEvent(SessionId SessionId, UserAccountId UserAccountId, string Reason) : DomainEvent;

/// <summary>Raised by <c>Unlock</c>.</summary>
public sealed record AccountUnlockedDomainEvent(UserAccountId UserAccountId) : DomainEvent;
