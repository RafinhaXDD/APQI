using BookExchange.Application.Shared;

namespace BookExchange.Api.Infrastructure;

internal static class ServiceResults
{
    public static IResult ToResult<T>(this ServiceResult<T> result, Func<T, IResult> onSuccess) => result.Failure switch
    {
        null => onSuccess(result.Value!),
        ServiceFailure.NotFound => Problems.Status(StatusCodes.Status404NotFound, result.Message!, result.Code!),
        ServiceFailure.Forbidden => Problems.Status(StatusCodes.Status403Forbidden, result.Message!, result.Code!),
        ServiceFailure.Invalid => Problems.Validation(
            new Dictionary<string, string[]> { [result.Field ?? "body"] = [result.Message!] }, result.Code!),
        ServiceFailure.Unavailable => Problems.Status(StatusCodes.Status503ServiceUnavailable, result.Message!, result.Code!),
        _ => throw new InvalidOperationException($"Unhandled failure {result.Failure}."),
    };
}
