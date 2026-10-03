using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>BC-01-IAM-and-UAM.md §10.1 US-3.4.3-04 AC-06 — adds the session's refresh token to the revocation list and revokes the session.</summary>
public sealed record RevokeTokenCommand(Guid UserId, Guid SessionId) : IRequest<Result>;

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.SessionId).NotEmpty();
    }
}

public sealed class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IUserAccountRepository _userAccounts;
    private readonly IIamUnitOfWork _unitOfWork;
    private readonly IRevokedTokenStore _revokedTokens;
    private readonly TimeProvider _timeProvider;

    public RevokeTokenCommandHandler(
        IUserAccountRepository userAccounts, IIamUnitOfWork unitOfWork, IRevokedTokenStore revokedTokens, TimeProvider timeProvider)
    {
        _userAccounts = userAccounts;
        _unitOfWork = unitOfWork;
        _revokedTokens = revokedTokens;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var accountId = new UserAccountId(request.UserId);
        var account = await _userAccounts.GetByIdAsync(accountId, cancellationToken);

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
