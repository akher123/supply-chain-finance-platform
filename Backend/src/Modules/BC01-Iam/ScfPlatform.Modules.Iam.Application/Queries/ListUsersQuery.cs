using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Queries;

/// <summary>BC-01-IAM-and-UAM.md §10.2 US-3.1.4-01 AC-01/02/03.</summary>
public sealed record ListUsersQuery(
    string? SearchText,
    UserRole? Role,
    AccountStatus? Status,
    DateTime? RegisteredFromUtc,
    DateTime? RegisteredToUtc,
    bool? IdentityVerified,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<UserListItemDto>>>;

public sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, Result<PagedResult<UserListItemDto>>>
{
    private readonly IUserAccountRepository _userAccounts;

    public ListUsersQueryHandler(IUserAccountRepository userAccounts) => _userAccounts = userAccounts;

    public async Task<Result<PagedResult<UserListItemDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var criteria = new UserSearchCriteria(
            request.SearchText, request.Role, request.Status, request.RegisteredFromUtc, request.RegisteredToUtc, request.IdentityVerified, request.Page, request.PageSize);

        var (items, total) = await _userAccounts.SearchAsync(criteria, cancellationToken);

        var dtos = items
            .Select(a => new UserListItemDto(a.Id.Value, a.Credential.Email.Value, MobileMasking.Mask(a.Credential.Mobile.Value), a.Role, a.Status, a.CreatedOnUtc, a.IdentityVerified))
            .ToList();

        return Result.Success(new PagedResult<UserListItemDto>(dtos, total, request.Page, request.PageSize));
    }
}
