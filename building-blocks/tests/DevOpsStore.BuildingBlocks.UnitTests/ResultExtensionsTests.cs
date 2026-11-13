using DevOpsStore.BuildingBlocks.Results;
using DevOpsStore.BuildingBlocks.Web;
using Microsoft.AspNetCore.Http;

namespace DevOpsStore.BuildingBlocks.UnitTests;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.BadRequest, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    public void ToProblem_MapsErrorTypeToStatusCode(ErrorType type, int expectedStatus)
    {
        var result = new Error(type, "Title", "Detail").ToProblem();

        Assert.Equal(expectedStatus, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public void ToProblem_Validation_Returns400WithErrors()
    {
        var error = new ValidationBuilder().AddIf(true, "name", "Required").ToError()!;

        var result = error.ToProblem();

        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public void ToCreated_Success_Returns201()
    {
        Result<string> result = "value";

        var http = result.ToCreated(v => $"/items/{v}");

        Assert.Equal(StatusCodes.Status201Created, ((IStatusCodeHttpResult)http).StatusCode);
    }

    [Fact]
    public void ToNoContent_Success_Returns204()
    {
        var http = Result.Success().ToNoContent();

        Assert.Equal(StatusCodes.Status204NoContent, ((IStatusCodeHttpResult)http).StatusCode);
    }
}
