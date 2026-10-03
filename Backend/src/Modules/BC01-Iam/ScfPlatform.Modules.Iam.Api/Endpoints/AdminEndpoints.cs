using ScfPlatform.Modules.Iam.Api.Authentication;
using ScfPlatform.Modules.Iam.Application.Admin;
using ScfPlatform.Modules.Iam.Application.Queries;
using ScfPlatform.Modules.Iam.Domain.Enums;
using MediatR;

namespace ScfPlatform.Modules.Iam.Api.Endpoints;

/// <summary>BC-01-IAM-and-UAM.md §12 — admin user-account management. Every route requires the <c>users:manage</c> permission (§6.2 invariant, RBAC).</summary>
public static class AdminEndpoints
{
    private const string ManageUsersPermission = "users:manage";

    public sealed record RejectRequest(string Reason);

    public sealed record SuspendRequest(string Reason);

    public sealed record AssignRoleRequest(UserRole Role);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/identity/admin").RequireAuthorization();

        admin.MapGet("/users", async ([AsParameters] ListUsersRequest request, ISender sender) =>
        {
            var result = await sender.Send(new ListUsersQuery(
                request.SearchText, request.Role, request.Status, request.RegisteredFromUtc, request.RegisteredToUtc,
                request.IdentityVerified, request.Page ?? 1, request.PageSize ?? 20));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapGet("/users/{id:guid}", async (Guid id, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new GetUserAsAdminQuery(http.User.UserId(), id));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/approve", async (Guid id, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminApproveEmployerCommand(http.User.UserId(), id));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/reject", async (Guid id, RejectRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminRejectEmployerCommand(http.User.UserId(), id, request.Reason));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/suspend", async (Guid id, SuspendRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminSuspendUserCommand(http.User.UserId(), id, request.Reason));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/reinstate", async (Guid id, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminReinstateUserCommand(http.User.UserId(), id));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/deactivate", async (Guid id, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminDeactivateUserCommand(http.User.UserId(), id));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/unlock", async (Guid id, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminUnlockAccountCommand(http.User.UserId(), id));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/password-reset", async (Guid id, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AdminIssuePasswordResetCommand(http.User.UserId(), id));

            return result.IsSuccess ? Results.Accepted() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapPost("/users/{id:guid}/role", async (Guid id, AssignRoleRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new AssignRoleCommand(http.User.UserId(), id, request.Role));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        admin.MapGet("/audit", async ([AsParameters] AdminAuditRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetAdminActionLogQuery(
                request.AdminUserId, request.TargetUserId, request.ActionType, request.FromUtc, request.ToUtc,
                request.Page ?? 1, request.PageSize ?? 20));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequirePermission(ManageUsersPermission);

        return app;
    }

    public sealed record ListUsersRequest(
        string? SearchText, UserRole? Role, AccountStatus? Status, DateTime? RegisteredFromUtc, DateTime? RegisteredToUtc,
        bool? IdentityVerified, int? Page, int? PageSize);

    public sealed record AdminAuditRequest(Guid? AdminUserId, Guid? TargetUserId, AdminActionType? ActionType, DateTime? FromUtc, DateTime? ToUtc, int? Page, int? PageSize);
}
