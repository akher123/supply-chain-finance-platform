using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Queries;

/// <summary>BC-01-IAM-and-UAM.md §10.2 US-3.1.5-04 AC-07.</summary>
public sealed record GetMySessionsQuery(Guid UserId, Guid CurrentSessionId) : IRequest<Result<IReadOnlyList<SessionDto>>>;

public sealed class GetMySessionsQueryValidator : AbstractValidator<GetMySessionsQuery>
{
    public GetMySessionsQueryValidator() => RuleFor(q => q.UserId).NotEmpty();
}

public sealed class GetMySessionsQueryHandler : IRequestHandler<GetMySessionsQuery, Result<IReadOnlyList<SessionDto>>>
{
    private readonly IUserAccountRepository _userAccounts;

    public GetMySessionsQueryHandler(IUserAccountRepository userAccounts) => _userAccounts = userAccounts;

    public async Task<Result<IReadOnlyList<SessionDto>>> Handle(GetMySessionsQuery request, CancellationToken cancellationToken)
    {
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure<IReadOnlyList<SessionDto>>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var sessions = account.Sessions
            .Where(s => s.IsActive)
            .Select(s => new SessionDto(s.Id.Value, s.Channel, s.DeviceFingerprint.Value, s.IssuedOnUtc, s.LastSeenUtc, s.Id.Value == request.CurrentSessionId))
            .ToList();

        return Result.Success<IReadOnlyList<SessionDto>>(sessions);
    }
}
