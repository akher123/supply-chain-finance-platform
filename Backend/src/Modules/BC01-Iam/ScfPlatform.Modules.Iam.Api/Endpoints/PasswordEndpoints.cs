using ScfPlatform.Modules.Iam.Api.Authentication;
using ScfPlatform.Modules.Iam.Application.PasswordManagement;
using MediatR;

namespace ScfPlatform.Modules.Iam.Api.Endpoints;

/// <summary>BC-01-IAM-and-UAM.md §12 — self-service password reset (anonymous) and change (authenticated).</summary>
public static class PasswordEndpoints
{
    public sealed record ResetRequestRequest(string Identifier);

    public sealed record ResetVerifyRequest(string Identifier, string Otp);

    public sealed record ResetCompleteRequest(string ResetToken, string NewPassword);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public static IEndpointRouteBuilder MapPasswordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/identity/password/reset-request", async (ResetRequestRequest request, ISender sender) =>
        {
            var result = await sender.Send(new RequestPasswordResetCommand(request.Identifier));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/password/reset-verify", async (ResetVerifyRequest request, ISender sender) =>
        {
            var result = await sender.Send(new VerifyPasswordResetOtpCommand(request.Identifier, request.Otp));

            return result.IsSuccess ? Results.Ok(result.Value) : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/password/reset", async (ResetCompleteRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CompletePasswordResetCommand(request.ResetToken, request.NewPassword));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).AllowAnonymous();

        app.MapPost("/api/identity/password/change", async (ChangePasswordRequest request, HttpContext http, ISender sender) =>
        {
            var result = await sender.Send(new ChangePasswordCommand(http.User.UserId(), request.CurrentPassword, request.NewPassword));

            return result.IsSuccess ? Results.Ok() : ResultHttpMapping.ToProblem(result.Error);
        }).RequireAuthorization();

        return app;
    }
}
