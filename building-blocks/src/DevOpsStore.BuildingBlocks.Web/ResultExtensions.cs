using DevOpsStore.BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;

namespace DevOpsStore.BuildingBlocks.Web;

/// <summary>Maps application <see cref="Result"/> objects to HTTP responses (RFC 9457 problem details on failure).</summary>
public static class ResultExtensions
{
    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? Microsoft.AspNetCore.Http.Results.Ok(result.Value) : result.Error!.ToProblem();

    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Microsoft.AspNetCore.Http.Results.Created(location(result.Value), result.Value) : result.Error!.ToProblem();

    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? Microsoft.AspNetCore.Http.Results.NoContent() : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error) => error.Type switch
    {
        ErrorType.Validation => Microsoft.AspNetCore.Http.Results.ValidationProblem(
            error.ValidationErrors?.ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, string[]>()),
        ErrorType.NotFound => Problem(error, StatusCodes.Status404NotFound),
        ErrorType.Conflict => Problem(error, StatusCodes.Status409Conflict),
        ErrorType.Unavailable => Problem(error, StatusCodes.Status503ServiceUnavailable),
        ErrorType.Unauthorized => Problem(error, StatusCodes.Status401Unauthorized),
        ErrorType.Forbidden => Problem(error, StatusCodes.Status403Forbidden),
        _ => Problem(error, StatusCodes.Status400BadRequest),
    };

    private static IResult Problem(Error error, int statusCode) =>
        Microsoft.AspNetCore.Http.Results.Problem(error.Detail, statusCode: statusCode, title: error.Title);
}
