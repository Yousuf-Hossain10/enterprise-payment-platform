using BuildingBlocks.Common;
using NSubstitute;
using Payment.Application;
using Payment.Domain;

namespace Payment.Tests;

public class CapturePaymentCommandHandlerTests
{
    private static CapturePaymentCommandHandler CreateSut(
        out IPaymentRepository payments, out IWalletClient walletClient)
    {
        payments = Substitute.For<IPaymentRepository>();
        walletClient = Substitute.For<IWalletClient>();
        return new CapturePaymentCommandHandler(payments, walletClient, new CapturePaymentCommandValidator());
    }

    private static Payment.Domain.Payment SamplePayment(Guid id, Guid accountId, decimal amount) => new()
    {
        Id = id,
        AccountId = accountId,
        Amount = amount,
        Reference = "order-1",
        IdempotencyKey = "seed-key",
        CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task Succeeds_AndCapturesPayment_WhenWalletDebitSucceeds()
    {
        var paymentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var payments, out var walletClient);
        var payment = SamplePayment(paymentId, accountId, 40m);
        payments.GetByIdAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        walletClient.DebitAsync(
                accountId, 40m, "idem-1", paymentId.ToString(), "token-1", Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Success(60m));

        var result = await sut.HandleAsync(
            new CapturePaymentCommand(paymentId, "idem-1", "token-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Captured, result.Value!.Status);
        await payments.Received(1).SaveAsync(payment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_AndMarksPaymentFailed_WhenWalletDebitFails()
    {
        var paymentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var payments, out var walletClient);
        var payment = SamplePayment(paymentId, accountId, 40m);
        payments.GetByIdAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        walletClient.DebitAsync(
                accountId, 40m, "idem-1", paymentId.ToString(), null, Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Failure("Insufficient funds."));

        var result = await sut.HandleAsync(
            new CapturePaymentCommand(paymentId, "idem-1", BearerToken: null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("Insufficient funds.", payment.FailureReason);
        await payments.Received(1).SaveAsync(payment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_WhenPaymentDoesNotExist()
    {
        var paymentId = Guid.NewGuid();
        var sut = CreateSut(out var payments, out var walletClient);
        payments.GetByIdAsync(paymentId, Arg.Any<CancellationToken>()).Returns((Payment.Domain.Payment?)null);

        var result = await sut.HandleAsync(
            new CapturePaymentCommand(paymentId, "idem-1", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        await walletClient.DidNotReceiveWithAnyArgs().DebitAsync(
            default, default, default!, default!, default, default);
        await payments.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task Fails_WithoutSaving_WhenPaymentIsNotInCreatedStatus()
    {
        var paymentId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var sut = CreateSut(out var payments, out var walletClient);
        var payment = SamplePayment(paymentId, accountId, 40m);
        payment.MarkAuthorized();
        payment.MarkCaptured();
        payments.GetByIdAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);

        var result = await sut.HandleAsync(
            new CapturePaymentCommand(paymentId, "idem-1", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentStatus.Captured, payment.Status);
        await walletClient.DidNotReceiveWithAnyArgs().DebitAsync(
            default, default, default!, default!, default, default);
        await payments.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Fails_ForEmptyIdempotencyKey(string idempotencyKey)
    {
        var sut = CreateSut(out _, out _);

        var result = await sut.HandleAsync(
            new CapturePaymentCommand(Guid.NewGuid(), idempotencyKey, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Fails_ForEmptyPaymentId()
    {
        var sut = CreateSut(out _, out _);

        var result = await sut.HandleAsync(
            new CapturePaymentCommand(Guid.Empty, "idem-1", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
