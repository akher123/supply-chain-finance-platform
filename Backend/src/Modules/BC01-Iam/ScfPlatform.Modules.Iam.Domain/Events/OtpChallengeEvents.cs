using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Domain.Events;

// Internal domain events (BC-01-IAM-and-UAM.md §8.2) raised by OtpChallenge — handled in-process
// within this module only; never published to the outbox.

/// <summary>Raised by <c>OtpChallenge.Issue</c>. A handler reacts by calling the <c>OtpDeliveryPort</c> (§9.2) with the plaintext code, which this event deliberately does not carry (the aggregate only ever stores/raises the hash).</summary>
public sealed record OtpIssuedDomainEvent(OtpChallengeId ChallengeId, UserAccountId UserAccountId, OtpPurpose Purpose, DateTime ExpiresOnUtc) : DomainEvent;

/// <summary>Raised by <c>OtpChallenge.Verify</c> on a successful match.</summary>
public sealed record OtpVerifiedDomainEvent(OtpChallengeId ChallengeId, UserAccountId UserAccountId, OtpPurpose Purpose) : DomainEvent;
