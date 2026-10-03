using ScfPlatform.Modules.Iam.Api.Authentication;
using ScfPlatform.Modules.Iam.Application.Queries;
using MediatR;

namespace ScfPlatform.Modules.Iam.Api.Endpoints;

/// <summary>BC-01-IAM-and-UAM.md §12 — the authenticated caller's own account/session views.</summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/identity/me", async (HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new GetMyAccountQuery(http.User.UserId()));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        app.MapGet("/api/identity/me/sessions", async (HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new GetMySessionsQuery(http.User.UserId(), http.User.SessionId()));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        return app;
    }
}
