using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Domain.Services;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.PasswordManagement;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.5-03 — self-service, authenticated password change.</summary>
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(10);
    }
}

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBreachCheckPort _breachCheck;
    private readonly TimeProvider _timeProvider;

    public ChangePasswordCommandHandler(
        IUserAccountRepository userAccounts,
        IIamUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IBreachCheckPort breachCheck,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _breachCheck = breachCheck;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var currentRaw = RawPassword.Create(request.CurrentPassword);

        if (currentRaw.IsFailure || !_passwordHasher.Verify(currentRaw.Value, account.Credential.PasswordHash))
        {
            return Result.Failure(Error.Unauthorized("E-CHANGE-PASSWORD-INVALID-CURRENT", "The current password is incorrect."));
        }

        var newRaw = RawPassword.Create(request.NewPassword);

        if (newRaw.IsFailure)
        {
            return newRaw;
        }

        var policyResult = PasswordPolicyService.Validate(newRaw.Value, PasswordPolicyContext.Reset);

        if (policyResult.IsFailure)
        {
            return policyResult;
        }

        if (await _breachCheck.IsBreachedAsync(newRaw.Value, cancellationToken))
        {
            return Result.Failure(Error.Validation(IamErrorCodes.ResetPasswordBreached, "This password has appeared in a known data breach."));
        }

        var newHash = _passwordHasher.Hash(newRaw.Value);
        var changeResult = account.ChangePassword(newHash, nowUtc);

        if (changeResult.IsFailure)
        {
            return changeResult;
        }

        _userAccounts.Update(account);
        _unitOfWork.EnqueueIntegrationEvent(new PasswordResetIntegrationEvent(account.Id.Value, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
