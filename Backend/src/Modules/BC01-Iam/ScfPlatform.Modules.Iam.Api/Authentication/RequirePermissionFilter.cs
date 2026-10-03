using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.Modules.Iam.Api.Authentication;

/// <summary>BC-01-IAM-and-UAM.md §12: "Admin endpoints additionally require the users:manage permission; RBAC violations return 403 with E-UNAUTHORIZED-ROLE / E-FORBIDDEN."</summary>
public sealed class RequirePermissionFilter : IEndpointFilter
{
    private readonly string _permission;

    public RequirePermissionFilter(string permission) => _permission = permission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.User.HasPermission(_permission))
        {
            return ResultHttpMapping.ToProblem(Error.Forbidden("E-FORBIDDEN", $"This action requires the '{_permission}' permission."));
        }

        return await next(context);
    }
}

public static class RequirePermissionFilterExtensions
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission) =>
        builder.AddEndpointFilter(new RequirePermissionFilter(permission));
}
