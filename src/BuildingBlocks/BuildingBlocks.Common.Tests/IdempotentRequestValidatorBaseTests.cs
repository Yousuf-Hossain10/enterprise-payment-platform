namespace BuildingBlocks.Common.Tests;

public class IdempotentRequestValidatorBaseTests
{
    private record TestRequest(string IdempotencyKey) : IIdempotentRequest;

    private class TestRequestValidator : IdempotentRequestValidatorBase<TestRequest>;

    [Fact]
    public void Fails_WhenIdempotencyKeyIsEmpty()
    {
        var validator = new TestRequestValidator();

        var result = validator.Validate(new TestRequest(""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TestRequest.IdempotencyKey));
    }

    [Fact]
    public void Passes_WhenIdempotencyKeyIsPresent()
    {
        var validator = new TestRequestValidator();

        var result = validator.Validate(new TestRequest("a-real-key"));

        Assert.True(result.IsValid);
    }
}
