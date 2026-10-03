using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Application.Tokens;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.5-04 — logout-everywhere.</summary>
public sealed record LogoutAllSessionsCommand(Guid UserId) : IRequest<Result>;

public sealed class LogoutAllSessionsCommandValidator : AbstractValidator<LogoutAllSessionsCommand>
{
    public LogoutAllSessionsCommandValidator() => RuleFor(c => c.UserId).NotEmpty();
}

public sealed class LogoutAllSessionsCommandHandler : IRequestHandler<LogoutAllSessionsCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IRevokedTokenStore _revokedTokens;
    private readonly TimeProvider _timeProvider;

    public LogoutAllSessionsCommandHandler(
        IUserAccountRepository userAccounts, IIamUnitOfWork unitOfWork, IRevokedTokenStore revokedTokens, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _revokedTokens = revokedTokens;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(LogoutAllSessionsCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        await SessionRevocationSupport.RevokeAllAsync(account, "logout-all", nowUtc, _unitOfWork, _revokedTokens, cancellationToken);

        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
