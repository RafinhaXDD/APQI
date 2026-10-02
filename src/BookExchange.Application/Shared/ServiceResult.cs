namespace BookExchange.Application.Shared;

public enum ServiceFailure
{
    NotFound,
    Forbidden,
    Invalid,
    Unavailable,
}

/// <summary>Outcome of a use case; the API maps <see cref="Failure"/> to a status code and <see cref="Code"/> to ProblemDetails.</summary>
public sealed record ServiceResult<T>(T? Value, ServiceFailure? Failure, string? Code, string? Message, string? Field)
{
    public bool Succeeded => Failure is null;
}

public static class ServiceResult
{
    public static ServiceResult<T> Ok<T>(T value) => new(value, null, null, null, null);

    public static ServiceResult<T> NotFound<T>(string code = ErrorCodes.NotFound) =>
        new(default, ServiceFailure.NotFound, code, "Not found.", null);

    public static ServiceResult<T> Forbidden<T>() =>
        new(default, ServiceFailure.Forbidden, ErrorCodes.Forbidden, "You can't change this.", null);

    public static ServiceResult<T> Invalid<T>(string code, string message, string field) =>
        new(default, ServiceFailure.Invalid, code, message, field);

    public static ServiceResult<T> Unavailable<T>(string code, string message) =>
        new(default, ServiceFailure.Unavailable, code, message, null);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
