namespace BuildingBlocks.Common.Tests;

public class ResultTests
{
    [Fact]
    public void Success_SetsIsSuccessTrueAndValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_SetsIsSuccessFalseAndError()
    {
        var result = Result<int>.Failure("something went wrong");

        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
        Assert.Equal("something went wrong", result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_RejectsNullOrWhitespaceError(string? error)
    {
        Assert.ThrowsAny<ArgumentException>(() => Result<int>.Failure(error!));
    }
}
