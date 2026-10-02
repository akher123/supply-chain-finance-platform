using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Domain.Enums;
using ScfPlatform.Modules.Iam.Domain.Ids;

namespace ScfPlatform.Modules.Iam.Domain.Events;

// Internal domain events (BC-01-IAM-and-UAM.md §8.2) raised by UserAccount's MFA/admin behaviors.

/// <summary>Raised by <c>EnableMfa</c>.</summary>
public sealed record MfaEnabledDomainEvent(UserAccountId UserAccountId, MfaMethod Method) : DomainEvent;

/// <summary>Raised by <c>DisableMfa</c>.</summary>
public sealed record MfaDisabledDomainEvent(UserAccountId UserAccountId) : DomainEvent;

/// <summary>Raised by <c>RedeemBackupCode</c>.</summary>
public sealed record BackupCodeRedeemedDomainEvent(UserAccountId UserAccountId, BackupCodeId BackupCodeId) : DomainEvent;

/// <summary>Raised by <c>ApplyGovernmentIdentityVerified</c>.</summary>
public sealed record IdentityVerificationAppliedDomainEvent(UserAccountId UserAccountId, string Registry) : DomainEvent;
