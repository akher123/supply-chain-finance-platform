using System.Security.Cryptography;
using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Admin;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.4-01 AC-09 — admin-assisted password reset; sends a reset link to the user's registered email.</summary>
public sealed record AdminIssuePasswordResetCommand(Guid AdminUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class AdminIssuePasswordResetCommandValidator : AbstractValidator<AdminIssuePasswordResetCommand>
{
    public AdminIssuePasswordResetCommandValidator()
    {
        RuleFor(c => c.AdminUserId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
    }
}

public sealed class AdminIssuePasswordResetCommandHandler : IRequestHandler<AdminIssuePasswordResetCommand, Result>
{
    private static readonly TimeSpan ResetTokenTtl = TimeSpan.FromMinutes(15);

    private readonly IUserAccountRepository _userAccounts;
    private readonly IAdminActionLogRepository _adminActionLogs;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IOtpDeliveryPort _otpDelivery;
    private readonly TimeProvider _timeProvider;

    public AdminIssuePasswordResetCommandHandler(
        IUserAccountRepository userAccounts,
        IAdminActionLogRepository adminActionLogs,
        IIamUnitOfWork unitOfWork,
        IOtpDeliveryPort otpDelivery,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _adminActionLogs = adminActionLogs;
        _unitOfWork = unitOfWork;
        _otpDelivery = otpDelivery;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AdminIssuePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var loadResult = await AdminActionSupport.LoadTargetAsync(_userAccounts, request.TargetUserId, cancellationToken);

        if (loadResult.IsFailure)
        {
            return loadResult;
        }

        var account = loadResult.Value;
        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        account.IssuePasswordResetToken(OtpCodeGenerator.Hash(resetToken), nowUtc, nowUtc.Add(ResetTokenTtl));

        _userAccounts.Update(account);
        await AdminActionSupport.RecordAsync(_adminActionLogs, request.AdminUserId, AdminActionType.PasswordResetIssued, request.TargetUserId, null, nowUtc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reuses the OTP delivery port to email the reset token/link — this module has no
        // separate "send link" port; the stub adapter just logs it either way (§9.2 "for the
        // exercise" stubbing allowance).
        await _otpDelivery.SendAsync(account.Credential.Email.Value, resetToken, OtpPurpose.PasswordReset, cancellationToken);

        return Result.Success();
    }
}
