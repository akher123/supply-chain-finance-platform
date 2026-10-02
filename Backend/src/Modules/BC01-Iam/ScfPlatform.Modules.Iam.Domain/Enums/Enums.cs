namespace ScfPlatform.Modules.Iam.Domain.Enums;

// Module enums (BC-01-IAM-and-UAM.md §2, §5).

/// <summary>One primary role per account, assigned at provisioning (BC-01 §2). Employer sub-roles
/// (Owner/Recruiter/Admin) are explicit permission grants on top of <see cref="Employer"/>, not
/// separate top-level roles (§7.2, appendix).</summary>
public enum UserRole
{
    JobSeeker,
    Employer,
    ThirdPartyPortal,
    MoLAdministrator,
}

/// <summary>The account lifecycle state machine (BC-01 §2, §6.2 invariant #1).</summary>
public enum AccountStatus
{
    PendingActivation,
    Active,
    Suspended,
    Deactivated,
}

/// <summary>What an <see cref="Aggregates.OtpChallenge"/> is for (BC-01 §5.2).</summary>
public enum OtpPurpose
{
    Activation,
    Mfa,
    PasswordReset,
}

/// <summary>An <see cref="Aggregates.OtpChallenge"/>'s own lifecycle (BC-01 §5.2, §6.3).</summary>
public enum OtpStatus
{
    Issued,
    Verified,
    Expired,
    Locked,
}

/// <summary>The second factor a user can enrol (BC-01 §2, §5.1).</summary>
public enum MfaMethod
{
    None,
    Totp,
    SmsOtp,
}

/// <summary>Where a <see cref="Entities.Session"/> was created from (BC-01 §5.1).</summary>
public enum SessionChannel
{
    Web,
    Mobile,
    Api,
}

/// <summary>An <see cref="Entities.AdminActionLog"/> row's action type (BC-01 §5.3).</summary>
public enum AdminActionType
{
    ApprovedEmployer,
    RejectedEmployer,
    Suspended,
    Reinstated,
    Deactivated,
    Unlocked,
    PasswordResetIssued,
    RoleAssigned,
    Viewed,
}
