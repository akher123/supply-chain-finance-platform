using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Ids;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Queries;

/// <summary>BC-01-IAM-and-UAM.md §10.2 US-3.1.5-04.</summary>
public sealed record GetMyAccountQuery(Guid UserId) : IRequest<Result<AccountDto>>;

public sealed class GetMyAccountQueryValidator : AbstractValidator<GetMyAccountQuery>
{
    public GetMyAccountQueryValidator() => RuleFor(q => q.UserId).NotEmpty();
}

public sealed class GetMyAccountQueryHandler : IRequestHandler<GetMyAccountQuery, Result<AccountDto>>
{
    private readonly IUserAccountRepository _userAccounts;

    public GetMyAccountQueryHandler(IUserAccountRepository userAccounts) => _userAccounts = userAccounts;

    public async Task<Result<AccountDto>> Handle(GetMyAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await _userAccounts.GetByIdAsync(new UserAccountId(request.UserId), cancellationToken);

        if (account is null)
        {
            return Result.Failure<AccountDto>(Error.NotFound(IamErrorCodes.AccountNotFound, "No such account."));
        }

        return Result.Success(new AccountDto(
            account.Id.Value,
            account.Credential.Email.Value,
            MobileMasking.Mask(account.Credential.Mobile.Value),
            account.Role,
            account.Status,
            account.Mfa.Enabled,
            account.IdentityVerified));
    }
}
