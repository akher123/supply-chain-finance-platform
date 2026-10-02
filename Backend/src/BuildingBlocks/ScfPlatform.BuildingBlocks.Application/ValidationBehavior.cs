using ScfPlatform.BuildingBlocks.Domain;
using FluentValidation;
using MediatR;

namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Mediator pipeline step that runs every registered FluentValidation validator for
/// <typeparamref name="TRequest"/> before the handler executes. On failure it short-circuits
/// the pipeline and returns a <see cref="Result"/>/<see cref="Result{T}"/> validation failure
/// instead of throwing — expected failures never become exceptions
/// (Handover_Packages/00-Shared-Foundations.md §5). Registered once, centrally, for every
/// module's command/query handlers (§4.4 "one composition root").
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var failures = _validators
            .Select(validator => validator.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var message = string.Join(" | ", failures.Select(f => f.ErrorMessage));
        var error = Error.Validation("Validation.Failed", message);

        return BuildValidationFailureResponse(error);
    }

    private static TResponse BuildValidationFailureResponse(Error error)
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        // TResponse is Result<TValue> — build it via the generic Result.Failure<TValue> factory.
        var valueType = responseType.GetGenericArguments()[0];
        var failureFactory = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(valueType);

        return (TResponse)failureFactory.Invoke(null, [error])!;
    }
}
