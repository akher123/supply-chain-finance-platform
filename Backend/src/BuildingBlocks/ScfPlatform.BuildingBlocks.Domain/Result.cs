namespace ScfPlatform.BuildingBlocks.Domain;

/// <summary>
/// An expected success-or-failure outcome with no return value
/// (Handover_Packages/00-Shared-Foundations.md §5). Expected failures (validation,
/// invariant violations, not-found) return <see cref="Result"/>/<see cref="Result{T}"/>;
/// exceptions are reserved for truly exceptional or programmer errors.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry a non-empty error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// An expected success-or-failure outcome carrying a value on success:
/// <c>{ IsSuccess: bool, Value: T, Error: Error }</c>
/// (Handover_Packages/00-Shared-Foundations.md §5).
/// </summary>
/// <typeparam name="TValue">The type produced on success.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The success value. Throws if accessed on a failed result — always check
    /// <see cref="Result.IsSuccess"/> (or pattern-match) first.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
