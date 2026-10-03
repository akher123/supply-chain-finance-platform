using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.1.5-04 AC-06 — the caller's current session logout (API layer clears the session cookie).</summary>
public sealed record LogoutCommand(Guid UserId, Guid SessionId) : IRequest<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.SessionId).NotEmpty();
    }
}

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IRevokedTokenStore _revokedTokens;
    private readonly TimeProvider _timeProvider;

    public LogoutCommandHandler(
        IUserAccountRepository userAccounts, IIamUnitOfWork unitOfWork, IRevokedTokenStore revokedTokens, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _revokedTokens = revokedTokens;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var revokeResult = await SessionRevocationSupport.RevokeOneAsync(
            account, new SessionId(request.SessionId), "explicit", nowUtc, _unitOfWork, _revokedTokens, cancellationToken);

        if (revokeResult.IsFailure)
        {
            return revokeResult;
        }

        _userAccounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
