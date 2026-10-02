namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// Broad failure category — used only by the API layer to pick a default HTTP status /
/// problem-details `type` when a module's own §12 contract doesn't dictate one explicitly.
/// It is never a substitute for <see cref="Error.Code"/>: consumers (frontend, other
/// modules) must always branch on <c>Code</c>, never on this enum or on HTTP status
/// (Handover_Packages/00-Shared-Foundations.md — "Backend is authoritative"; §12 of every
/// BC-* package enumerates the exact codes that are the real, frozen contract).
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}

/// <summary>
/// An expected, non-exceptional failure: <c>{ Code: string, Message: string }</c>
/// (Handover_Packages/00-Shared-Foundations.md §5). <see cref="Code"/> is the frozen
/// contract each BC-* package's §12 enumerates — preserve it exactly through every layer.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
