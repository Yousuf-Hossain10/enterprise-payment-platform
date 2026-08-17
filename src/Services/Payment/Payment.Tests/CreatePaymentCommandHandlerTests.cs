using NSubstitute;
using Payment.Application;
using Payment.Domain;

namespace Payment.Tests;

public class CreatePaymentCommandHandlerTests
{
    private static CreatePaymentCommandHandler CreateSut(out IPaymentRepository payments)
    {
        payments = Substitute.For<IPaymentRepository>();
        return new CreatePaymentCommandHandler(payments, new CreatePaymentCommandValidator());
    }

    [Fact]
    public async Task Succeeds_AndAddsNewPayment_InCreatedStatus()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var payments);
        payments.GetByIdempotencyKeyAsync("idem-1", Arg.Any<CancellationToken>())
            .Returns((Payment.Domain.Payment?)null);

        var result = await sut.HandleAsync(
            new CreatePaymentCommand(accountId, 40m, "order-1", "idem-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Created, result.Value!.Status);
        Assert.Equal(accountId, result.Value.AccountId);
        Assert.Equal(40m, result.Value.Amount);
        Assert.Equal("order-1", result.Value.Reference);
        Assert.Equal("idem-1", result.Value.IdempotencyKey);
        payments.Received(1).Add(Arg.Is<Payment.Domain.Payment>(p =>
            p.AccountId == accountId && p.Amount == 40m && p.Reference == "order-1"));
        await payments.Received(1).SaveAsync(Arg.Any<Payment.Domain.Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsIdempotent_AndReturnsTheOriginalPayment_WithoutAddingASecondOne_OnRetry()
    {
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var payments);
        var existing = new Payment.Domain.Payment
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Amount = 40m,
            Reference = "order-1",
            IdempotencyKey = "idem-1",
            CreatedAtUtc = DateTime.UtcNow
        };
        payments.GetByIdempotencyKeyAsync("idem-1", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await sut.HandleAsync(
            new CreatePaymentCommand(accountId, 40m, "order-1", "idem-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(existing, result.Value);
        payments.DidNotReceiveWithAnyArgs().Add(default!);
        await payments.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Fails_ForNonPositiveAmount(decimal amount)
    {
        var sut = CreateSut(out _);

        var result = await sut.HandleAsync(
            new CreatePaymentCommand(Guid.NewGuid(), amount, "order-1", "idem-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Fails_ForEmptyReference()
    {
        var sut = CreateSut(out _);

        var result = await sut.HandleAsync(
            new CreatePaymentCommand(Guid.NewGuid(), 40m, "", "idem-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
