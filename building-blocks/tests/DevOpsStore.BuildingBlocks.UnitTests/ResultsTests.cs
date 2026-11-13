using DevOpsStore.BuildingBlocks.Results;

namespace DevOpsStore.BuildingBlocks.UnitTests;

public class ResultsTests
{
    [Fact]
    public void ImplicitConversionFromValue_IsSuccess()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ImplicitConversionFromError_IsFailure_AndValueThrows()
    {
        Result<int> result = Error.NotFound("missing");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ValidationBuilder_NoFailures_ReturnsNull()
    {
        Assert.Null(new ValidationBuilder().AddIf(false, "name", "Required").ToError());
    }

    [Fact]
    public void ValidationBuilder_CollectsMultipleMessagesPerField()
    {
        var error = new ValidationBuilder()
            .AddIf(true, "name", "Required")
            .AddIf(true, "name", "Too long")
            .AddIf(true, "price", "Must be positive")
            .ToError();

        Assert.Equal(ErrorType.Validation, error!.Type);
        Assert.Equal(["Required", "Too long"], error.ValidationErrors!["name"]);
        Assert.Single(error.ValidationErrors["price"]);
    }
}
