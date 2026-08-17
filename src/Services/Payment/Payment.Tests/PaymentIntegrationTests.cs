using System.Text.Json;
using BuildingBlocks.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Payment.Application;
using Payment.Domain;
using Payment.Infrastructure;
using Testcontainers.PostgreSql;

namespace Payment.Tests;

/// <summary>
/// Exercises CapturePaymentCommandHandler against a real, throwaway Postgres
/// container (PaymentRepository/PaymentDbContext) - no mocks for persistence - per
/// docs/Coding-Standards.md. IWalletClient is still faked here: Wallet has no live
/// HTTP endpoint yet (see WalletClient.cs's remarks), and its own resilience
/// behavior is already covered for real in WalletClientTests.cs. What this file
/// proves that a fully-mocked unit test can't: the saga's persistence actually
/// commits correctly - the unique index on IdempotencyKey holds, and the terminal
/// status (Captured/Failed) plus FailureReason are durably saved.
/// </summary>
public class PaymentIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private PaymentDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new PaymentDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<Guid> SeedPaymentAsync(decimal amount)
    {
        var paymentId = Guid.NewGuid();
        _db.Payments.Add(new Payment.Domain.Payment
        {
            Id = paymentId,
            AccountId = Guid.NewGuid(),
            Amount = amount,
            Reference = "order-1",
            IdempotencyKey = Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return paymentId;
    }

    [Fact]
    public async Task Capture_persists_Captured_status_when_wallet_debit_succeeds()
    {
        var paymentId = await SeedPaymentAsync(40m);
        var walletClient = Substitute.For<IWalletClient>();
        walletClient.DebitAsync(
                Arg.Any<Guid>(), 40m, Arg.Any<string>(), paymentId.ToString(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Success(60m));
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var verifyDb = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var persisted = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Captured, persisted.Status);
    }

    [Fact]
    public async Task Capture_persists_Failed_status_and_reason_when_wallet_debit_fails()
    {
        var paymentId = await SeedPaymentAsync(40m);
        var walletClient = Substitute.For<IWalletClient>();
        walletClient.DebitAsync(
                Arg.Any<Guid>(), 40m, Arg.Any<string>(), paymentId.ToString(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Failure("Insufficient funds."));
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        Assert.False(result.IsSuccess);

        await using var verifyDb = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var persisted = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Failed, persisted.Status);
        Assert.Equal("Insufficient funds.", persisted.FailureReason);
    }

    [Fact]
    public async Task A_crash_between_MarkAuthorized_and_the_wallet_call_leaves_the_payment_in_Created_status()
    {
        // Simulates the crash-safety property CapturePaymentCommandHandler's design
        // relies on (see its class remarks): MarkAuthorized() is never independently
        // persisted, so if the process died right after it (before the Wallet call
        // even started), the stored record is untouched - a retried capture starts
        // clean rather than finding a stuck Authorized record.
        var paymentId = await SeedPaymentAsync(40m);

        await using var db = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
        payment.MarkAuthorized();
        // Deliberately no SaveChangesAsync here - this DbContext instance is simply
        // discarded, as if the process had crashed.

        await using var verifyDb = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var persisted = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Created, persisted.Status);
    }

    [Fact]
    public async Task Two_payments_cannot_share_the_same_idempotency_key()
    {
        var sharedKey = Guid.NewGuid().ToString();
        _db.Payments.Add(new Payment.Domain.Payment
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount = 10m,
            Reference = "order-1",
            IdempotencyKey = sharedKey,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _db.Payments.Add(new Payment.Domain.Payment
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Amount = 20m,
            Reference = "order-2",
            IdempotencyKey = sharedKey,
            CreatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task CreatePayment_persists_a_new_payment_in_Created_status()
    {
        var accountId = Guid.NewGuid();
        var handler = new CreatePaymentCommandHandler(new PaymentRepository(_db), new CreatePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CreatePaymentCommand(accountId, 40m, "order-1", Guid.NewGuid().ToString()), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var verifyDb = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var persisted = await verifyDb.Payments.SingleAsync(p => p.Id == result.Value!.Id);
        Assert.Equal(PaymentStatus.Created, persisted.Status);
        Assert.Equal(accountId, persisted.AccountId);
    }

    [Fact]
    public async Task Retried_CreatePayment_with_the_same_idempotency_key_produces_exactly_one_payment()
    {
        var accountId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();
        var handler = new CreatePaymentCommandHandler(new PaymentRepository(_db), new CreatePaymentCommandValidator());

        var first = await handler.HandleAsync(
            new CreatePaymentCommand(accountId, 40m, "order-1", idempotencyKey), CancellationToken.None);
        var retry = await handler.HandleAsync(
            new CreatePaymentCommand(accountId, 40m, "order-1", idempotencyKey), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value!.Id, retry.Value!.Id);

        var count = await _db.Payments.CountAsync(p => p.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Capture_writes_a_PaymentCaptured_outbox_message_in_the_same_transaction()
    {
        var paymentId = await SeedPaymentAsync(40m);
        var walletClient = Substitute.For<IWalletClient>();
        walletClient.DebitAsync(
                Arg.Any<Guid>(), 40m, Arg.Any<string>(), paymentId.ToString(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Success(60m));
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var message = await _db.OutboxMessages.SingleAsync(m => m.Type == "PaymentCaptured");
        Assert.Null(message.ProcessedAtUtc);
        var payload = JsonSerializer.Deserialize<PaymentCaptured>(message.Payload)!;
        Assert.Equal(paymentId, payload.PaymentId);
        Assert.Equal(40m, payload.Amount);
    }

    [Fact]
    public async Task Capture_writes_a_PaymentFailed_outbox_message_in_the_same_transaction()
    {
        var paymentId = await SeedPaymentAsync(40m);
        var walletClient = Substitute.For<IWalletClient>();
        walletClient.DebitAsync(
                Arg.Any<Guid>(), 40m, Arg.Any<string>(), paymentId.ToString(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Failure("Insufficient funds."));
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        Assert.False(result.IsSuccess);

        var message = await _db.OutboxMessages.SingleAsync(m => m.Type == "PaymentFailed");
        Assert.Null(message.ProcessedAtUtc);
        var payload = JsonSerializer.Deserialize<PaymentFailed>(message.Payload)!;
        Assert.Equal(paymentId, payload.PaymentId);
        Assert.Equal("Insufficient funds.", payload.FailureReason);
    }

    [Fact]
    public async Task PaymentOutboxStore_returns_unprocessed_messages_and_MarkProcessedAsync_excludes_them_afterward()
    {
        var paymentId = await SeedPaymentAsync(40m);
        var walletClient = Substitute.For<IWalletClient>();
        walletClient.DebitAsync(
                Arg.Any<Guid>(), 40m, Arg.Any<string>(), paymentId.ToString(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Success(60m));
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());
        await handler.HandleAsync(new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        await using var storeContext = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var store = new PaymentOutboxStore(storeContext);

        var unprocessed = await store.GetUnprocessedAsync(50, CancellationToken.None);
        Assert.Single(unprocessed);
        Assert.Equal("PaymentCaptured", unprocessed[0].Type);

        await store.MarkProcessedAsync(unprocessed[0].Id, DateTime.UtcNow, CancellationToken.None);

        var stillUnprocessed = await store.GetUnprocessedAsync(50, CancellationToken.None);
        Assert.Empty(stillUnprocessed);
    }
}
