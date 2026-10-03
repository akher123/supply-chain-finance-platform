using ScfPlatform.BuildingBlocks.Domain;

namespace ScfPlatform.Modules.Iam.Api;

/// <summary>
/// Maps a <see cref="Result"/>/<see cref="Result{T}"/> failure to an HTTP response that preserves
/// <see cref="Error.Code"/> exactly (00-Shared-Foundations.md, "the API layer maps Result failures
/// to problem-details responses preserving Error.Code"). Every <c>Error.Code</c> BC-01-IAM-and-UAM.md
/// §12 pins to a specific status overrides the generic <see cref="ErrorType"/> default; anything not
/// in that table falls back to the ErrorType-based default.
/// </summary>
public static class ResultHttpMapping
{
    private static readonly Dictionary<string, int> StatusOverrides = new(StringComparer.Ordinal)
    {
        ["E-LOGIN-ACCOUNT-LOCKED"] = StatusCodes.Status423Locked,
        ["E-OTP-LOCKED"] = StatusCodes.Status423Locked,
        ["E-LOGIN-RATE-LIMITED"] = StatusCodes.Status429TooManyRequests,
        ["E-REG-RATE-LIMITED"] = StatusCodes.Status429TooManyRequests,
        ["E-RESET-RATE-LIMITED"] = StatusCodes.Status429TooManyRequests,
        ["E-OTP-EXPIRED"] = StatusCodes.Status410Gone,
        ["E-RESET-TOKEN-EXPIRED"] = StatusCodes.Status410Gone,
        ["E-RESET-PASSWORD-BREACHED"] = StatusCodes.Status422UnprocessableEntity,
    };

    public static IResult ToProblem(Error error)
    {
        var status = StatusOverrides.TryGetValue(error.Code, out var overridden) ? overridden : DefaultStatusFor(error.Type);

        return Results.Problem(
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    private static int DefaultStatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };
}
