namespace DevOpsStore.BuildingBlocks.Results;

public enum ErrorType
{
    Validation,
    BadRequest,
    NotFound,
    Conflict,
    Unavailable,
    Unauthorized,
    Forbidden,
}

/// <summary>An expected failure. The web layer maps <see cref="Type"/> to an HTTP status code.</summary>
public sealed record Error(
    ErrorType Type,
    string Title,
    string Detail,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorType.Validation, "One or more validation errors occurred.", "See the errors property for details.", errors);

    public static Error BadRequest(string title, string detail) => new(ErrorType.BadRequest, title, detail);

    public static Error NotFound(string detail) => new(ErrorType.NotFound, "Not found", detail);

    public static Error Conflict(string title, string detail) => new(ErrorType.Conflict, title, detail);

    public static Error Unavailable(string detail) => new(ErrorType.Unavailable, "Downstream service unavailable", detail);

    public static Error Unauthorized(string detail) => new(ErrorType.Unauthorized, "Unauthorized", detail);

    public static Error Forbidden(string detail) => new(ErrorType.Forbidden, "Forbidden", detail);
}
