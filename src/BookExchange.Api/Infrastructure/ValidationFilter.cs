using FluentValidation;

namespace BookExchange.Api.Infrastructure;

/// <summary>Runs the FluentValidation validator for the endpoint's <typeparamref name="T"/> argument (R-23).</summary>
internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return Problems.Validation(new Dictionary<string, string[]> { ["body"] = ["A request body is required."] });
        }

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        return result.IsValid ? await next(context) : Problems.Validation(result.ToDictionary());
    }
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
}
