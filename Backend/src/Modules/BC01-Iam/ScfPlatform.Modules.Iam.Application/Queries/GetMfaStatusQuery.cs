using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Queries;

/// <summary>
/// BC-01-IAM-and-UAM.md §10.2 US-3.1.5-02 AC-10. Note: neither §5.1's <c>UserAccount</c> member
/// table nor §11.1's schema carries an explicit "MFA last verified" timestamp — flagged in
/// BUILD_REPORT.md as a minor package gap; <see cref="MfaStatusDto.LastVerifiedOnUtc"/> is always
/// null until a future contract revision adds the backing column.
/// </summary>
public sealed record GetMfaStatusQuery(Guid UserId) : IRequest<Result<MfaStatusDto>>;

public sealed class GetMfaStatusQueryValidator : AbstractValidator<GetMfaStatusQuery>
{
    public GetMfaStatusQueryValidator() => RuleFor(q => q.UserId).NotEmpty();
}

public sealed class GetMfaStatusQueryHandler : IRequestHandler<GetMfaStatusQuery, Result<MfaStatusDto>>
{
    private readonly IUserAccountRepository _userAccounts;

    public GetMfaStatusQueryHandler(IUserAccountRepository userAccounts) => _userAccounts = userAccounts;

    public async Task<Result<MfaStatusDto>> Handle(GetMfaStatusQuery request, CancellationToken cancellationToken)
    {
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure<MfaStatusDto>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        var remaining = account.BackupCodes.Count(c => !c.IsUsed);

        return Result.Success(new MfaStatusDto(account.Mfa.Enabled, account.Mfa.Method, null, remaining));
    }
}
