using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Common;
using ScfPlatform.Modules.Iam.Domain.Enums;
using FluentValidation;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Queries;

/// <summary>BC-01-IAM-and-UAM.md §10.2 US-3.1.4-01 AC-11.</summary>
public sealed record GetAdminActionLogQuery(
    Guid? AdminUserId,
    Guid? TargetUserId,
    AdminActionType? ActionType,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<AdminActionDto>>>;

public sealed class GetAdminActionLogQueryValidator : AbstractValidator<GetAdminActionLogQuery>
{
    public GetAdminActionLogQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class GetAdminActionLogQueryHandler : IRequestHandler<GetAdminActionLogQuery, Result<PagedResult<AdminActionDto>>>
{
    private readonly IAdminActionLogRepository _adminActionLogs;

    public GetAdminActionLogQueryHandler(IAdminActionLogRepository adminActionLogs) => _adminActionLogs = adminActionLogs;

    public async Task<Result<PagedResult<AdminActionDto>>> Handle(GetAdminActionLogQuery request, CancellationToken cancellationToken)
    {
        var query = new AdminActionLogQuery(request.AdminUserId, request.TargetUserId, request.ActionType, request.FromUtc, request.ToUtc, request.Page, request.PageSize);
        var (items, total) = await _adminActionLogs.QueryAsync(query, cancellationToken);

        var dtos = items.Select(e => new AdminActionDto(e.Id, e.AdminUserId, e.ActionType, e.TargetUserId, e.Reason, e.OccurredOnUtc)).ToList();

        return Result.Success(new PagedResult<AdminActionDto>(dtos, total, request.Page, request.PageSize));
    }
}
