using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Services;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using ScfPlatform.Modules.Iam.Contracts;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.PasswordManagement;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.5-03 AC-05/06/07/08.</summary>
public sealed record CompletePasswordResetCommand(string ResetToken, string NewPassword) : IRequest<Result>;

public sealed class CompletePasswordResetCommandValidator : AbstractValidator<CompletePasswordResetCommand>
{
    public CompletePasswordResetCommandValidator()
    {
        RuleFor(c => c.ResetToken).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(10);
    }
}

public sealed class CompletePasswordResetCommandHandler : IRequestHandler<CompletePasswordResetCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBreachCheckPort _breachCheck;
    private readonly TimeProvider _timeProvider;

    public CompletePasswordResetCommandHandler(
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

    public async Task<Result> Handle(CompletePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = Common.OtpCodeGenerator.Hash(request.ResetToken);
        var account = await _userAccounts.GetByPasswordResetTokenHashAsync(tokenHash, cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.Validation(IamErrorCodes.ResetTokenInvalid, "This password-reset token is invalid, used, or unknown."));
        }

        var rawPasswordResult = RawPassword.Create(request.NewPassword);

        if (rawPasswordResult.IsFailure)
        {
            return rawPasswordResult;
        }

        var policyResult = PasswordPolicyService.Validate(rawPasswordResult.Value, PasswordPolicyContext.Reset);

        if (policyResult.IsFailure)
        {
            return policyResult;
        }

        if (await _breachCheck.IsBreachedAsync(rawPasswordResult.Value, cancellationToken))
        {
            return Result.Failure(Error.Validation(IamErrorCodes.ResetPasswordBreached, "This password has appeared in a known data breach."));
        }

        var newHash = _passwordHasher.Hash(rawPasswordResult.Value);
        var resetResult = account.CompletePasswordReset(tokenHash, newHash, nowUtc);

        if (resetResult.IsFailure)
        {
            return resetResult;
        }

        _userAccounts.Update(account);
        _unitOfWork.EnqueueIntegrationEvent(new PasswordResetIntegrationEvent(account.Id.Value, nowUtc));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
