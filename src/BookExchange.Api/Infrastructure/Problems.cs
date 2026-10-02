using BookExchange.Application.Shared;

namespace BookExchange.Api.Infrastructure;

/// <summary>ProblemDetails results with a stable <c>code</c> the frontend translates (R-22).</summary>
internal static class Problems
{
    public const string CodeKey = "code";

    public static IResult Status(int statusCode, string title, string code) =>
        Results.Problem(statusCode: statusCode, title: title, extensions: new Dictionary<string, object?> { [CodeKey] = code });

    /// <summary>422 with field errors; keys camelCased to match the JSON the client sent.</summary>
    public static IResult Validation(IDictionary<string, string[]> errors, string code = ErrorCodes.ValidationFailed) =>
        Results.ValidationProblem(
            errors.ToDictionary(e => CamelCase(e.Key), e => e.Value),
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "One or more fields are invalid.",
            extensions: new Dictionary<string, object?> { [CodeKey] = code });

    /// <summary>Default code by status, for problems produced by the framework (401 challenge, 404 route, …).</summary>
    public static string DefaultCode(int? statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => ErrorCodes.ValidationFailed,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        _ => ErrorCodes.ServerError,
    };

    private static string CamelCase(string key) =>
        string.Join('.', key.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
