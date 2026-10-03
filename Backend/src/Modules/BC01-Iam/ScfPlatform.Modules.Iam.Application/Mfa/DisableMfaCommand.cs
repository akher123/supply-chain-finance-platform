using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using ScfPlatform.Modules.Iam.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Mfa;

/// <summary>BC-01-IAM-and-UAM.md §10.1, US-3.1.5-02 — requires re-authentication (the current password) before disabling MFA.</summary>
public sealed record DisableMfaCommand(Guid UserId, string CurrentPassword) : IRequest<Result>;

public sealed class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CurrentPassword).NotEmpty();
    }
}

public sealed class DisableMfaCommandHandler : IRequestHandler<DisableMfaCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public DisableMfaCommandHandler(
        IUserAccountRepository userAccounts,
        IIamUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var rawPasswordResult = RawPassword.Create(request.CurrentPassword);

        if (rawPasswordResult.IsFailure || !_passwordHasher.Verify(rawPasswordResult.Value, account.Credential.PasswordHash))
        {
            return Result.Failure(Error.Unauthorized(IamErrorCodes.MfaReauthRequired, "Re-authentication failed."));
        }

        account.DisableMfa(nowUtc);
        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
