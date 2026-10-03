using ScfPlatform.Modules.Iam.Api.Authentication;
using ScfPlatform.Modules.Iam.Application.Mfa;
using ScfPlatform.Modules.Iam.Application.Queries;
using ScfPlatform.Modules.Iam.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ScfPlatform.Modules.Iam.Api.Endpoints;

/// <summary>BC-01-IAM-and-UAM.md §12 — MFA setup/status routes. All require authentication (a user managing their own MFA).</summary>
public static class MfaEndpoints
{
    public sealed record EnrollMfaRequest(MfaMethod Method);

    public sealed record ConfirmMfaEnrollmentRequest(MfaMethod Method, string SecretRef, string Code);

    public sealed record DisableMfaRequest(string CurrentPassword);

    public static IEndpointRouteBuilder MapMfaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/identity/mfa/enroll", async (EnrollMfaRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new EnrollMfaCommand(http.User.UserId(), request.Method));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        app.MapPost("/api/identity/mfa/enroll/confirm", async (ConfirmMfaEnrollmentRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new ConfirmMfaEnrollmentCommand(http.User.UserId(), request.Method, request.SecretRef, request.Code));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        app.MapDelete("/api/identity/mfa", async ([FromBody] DisableMfaRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new DisableMfaCommand(http.User.UserId(), request.CurrentPassword));

            return result.IsSuccess ? Results.NoContent() : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        app.MapGet("/api/identity/me/mfa", async (HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new GetMfaStatusQuery(http.User.UserId()));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        return app;
    }
}
