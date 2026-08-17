using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Payment.Application;
using Payment.Domain;
using Payment.Infrastructure;
using Polly;
using Polly.Extensions.Http;
using Testcontainers.PostgreSql;

namespace Payment.Tests;

/// <summary>
/// The saga's most important test (docs/Enterprise_Payment_Platform_Tutorial.md
/// Phase 7 - "analogous to Phase 6's concurrency test"): kill Wallet mid-flow and
/// assert the payment transitions to Failed rather than hanging in Authorized
/// indefinitely. Deliberately more than another mocked IWalletClient test (already
/// covered in CapturePaymentCommandHandlerTests.cs/PaymentIntegrationTests.cs) -
/// this runs a real, disposable Kestrel server standing in for Wallet, driving the
/// *real* WalletClient wired with the *same shape* of Polly retry+circuit-breaker
/// policies Payment.Api/Program.cs actually configures (shorter delays here, purely
/// for test speed - the composition being proven is retry-wraps-breaker, same as
/// production), against a real Postgres-backed saga. This is the fullest exercise
/// of the real HTTP/resilience stack short of running Wallet.Api itself, which
/// has no live debit endpoint yet.
/// </summary>
public class FaultInjectionTests : IAsyncLifetime
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

    private static async Task<WebApplication> StartStubWalletServerAsync(RequestDelegate handleDebit)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapPost("/api/v1/accounts/{accountId}/debit", handleDebit);
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Same policy shape as Payment.Api/Program.cs (retry wraps circuit breaker -
    /// each retry attempt is individually counted by the breaker, same as
    /// production), with much shorter delays so the test doesn't take minutes.
    /// </summary>
    private static WalletClient CreateResilientWalletClient(string baseUrl)
    {
        var breakerPolicy = HttpPolicyExtensions.HandleTransientHttpError().CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(10));

        var breakerHandler = new PolicyHttpMessageHandler(breakerPolicy) { InnerHandler = new HttpClientHandler() };
        var retryHandler = new PolicyHttpMessageHandler(retryPolicy) { InnerHandler = breakerHandler };

        var httpClient = new HttpClient(retryHandler) { BaseAddress = new Uri(baseUrl) };
        return new WalletClient(httpClient);
    }

    [Fact]
    public async Task Capture_transitions_to_Failed_notStuck_when_wallet_persistently_returns_500()
    {
        var paymentId = await SeedPaymentAsync(40m);
        await using var walletApp = await StartStubWalletServerAsync(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        var walletClient = CreateResilientWalletClient(walletApp.Urls.First());
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        Assert.False(result.IsSuccess);

        await using var verifyDb = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var persisted = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Failed, persisted.Status);
        Assert.NotNull(persisted.FailureReason);
    }

    [Fact]
    public async Task Capture_transitions_to_Failed_notStuck_when_wallet_is_killed_before_ever_responding()
    {
        var paymentId = await SeedPaymentAsync(40m);

        // Start a real server, capture its address, then kill it immediately -
        // every call the saga makes hits a genuinely dead endpoint, exactly like a
        // Wallet outage that started before this capture was even attempted.
        var walletApp = await StartStubWalletServerAsync(_ => Task.CompletedTask);
        var baseUrl = walletApp.Urls.First();
        await walletApp.StopAsync();
        await walletApp.DisposeAsync();

        var walletClient = CreateResilientWalletClient(baseUrl);
        var handler = new CapturePaymentCommandHandler(
            new PaymentRepository(_db), walletClient, new CapturePaymentCommandValidator());

        var result = await handler.HandleAsync(
            new CapturePaymentCommand(paymentId, Guid.NewGuid().ToString(), null), CancellationToken.None);

        Assert.False(result.IsSuccess);

        await using var verifyDb = new PaymentDbContext(
            new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        var persisted = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        Assert.Equal(PaymentStatus.Failed, persisted.Status);
    }

    [Fact]
    public async Task Capture_recovers_and_succeeds_once_wallet_comes_back_within_the_retry_window()
    {
        // The flip side of the fault-injection story: a transient outage that
        // resolves before retries are exhausted must NOT leave the payment failed -
        // the saga should capture successfully, same as a real momentary blip.
        var paymentId = await SeedPaymentAsync(40m);
        var attempt = 0;
        await using var walletApp = await StartStubWalletServerAsync(async ctx =>
        {
            attempt++;
            if (attempt < 2)
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return;
            }
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"balance\": 60.00}");
        });

        var walletClient = CreateResilientWalletClient(walletApp.Urls.First());
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
}
