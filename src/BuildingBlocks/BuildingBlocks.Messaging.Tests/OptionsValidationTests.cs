using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Messaging.Tests;

public class OptionsValidationTests
{
    private static bool IsValid(object options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);

    [Fact]
    public void RabbitMqOptions_Invalid_WhenRequiredFieldsMissing()
    {
        Assert.False(IsValid(new RabbitMqOptions { HostName = null!, UserName = null!, Password = null! }));
    }

    [Fact]
    public void RabbitMqOptions_Valid_WhenRequiredFieldsPresent()
    {
        Assert.True(IsValid(new RabbitMqOptions { HostName = "localhost", UserName = "user", Password = "pass" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void OutboxDispatcherOptions_Invalid_WhenBatchSizeOutOfRange(int batchSize)
    {
        Assert.False(IsValid(new OutboxDispatcherOptions { BatchSize = batchSize }));
    }

    [Fact]
    public void OutboxDispatcherOptions_Valid_WithDefaults()
    {
        Assert.True(IsValid(new OutboxDispatcherOptions()));
    }
}
