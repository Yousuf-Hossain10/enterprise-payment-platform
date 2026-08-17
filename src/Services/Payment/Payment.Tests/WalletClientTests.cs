using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;
using Payment.Infrastructure;
using Polly;
using Polly.Extensions.Http;

namespace Payment.Tests;

/// <summary>
/// Wallet has no live HTTP endpoint yet (see WalletClient.cs's remarks), so these
/// tests exercise WalletClient against a fake HttpMessageHandler rather than a real
/// Wallet instance - the only way to deterministically drive retry and
/// circuit-breaker behavior on demand anyway, which even a real server can't
/// reliably do. Retry and circuit-breaker are tested in isolation from each other
/// (rather than combined, as they're actually wired in Payment.Api/Program.cs) so
/// each test verifies one concern without depending on exact cross-policy counting.
/// </summary>
public class WalletClientTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
    };

    private static WalletClient CreateClient(FakeHttpMessageHandler handler, DelegatingHandler? policyHandler = null)
    {
        HttpMessageHandler pipeline = handler;
        if (policyHandler is not null)
        {
            policyHandler.InnerHandler = handler;
            pipeline = policyHandler;
        }

        var httpClient = new HttpClient(pipeline) { BaseAddress = new Uri("http://wallet.test") };
        return new WalletClient(httpClient);
    }

    [Fact]
    public async Task DebitAsync_ReturnsSuccessWithBalance_OnSuccessfulResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, new { balance = 60m }));
        var client = CreateClient(handler);

        var result = await client.DebitAsync(
            Guid.NewGuid(), 40m, "key-1", "ref-1", bearerToken: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(60m, result.Value);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_ReturnsFailureWithProblemDetail_OnBusinessFailure()
    {
        var handler = new FakeHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.Conflict, new { title = "Insufficient balance", detail = "Insufficient funds." }));
        var client = CreateClient(handler);

        var result = await client.DebitAsync(
            Guid.NewGuid(), 40m, "key-1", "ref-1", bearerToken: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Error);
        // A 409 isn't a transient HTTP error - no retry policy is attached in this
        // test, but the point stands: business failures aren't network failures.
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_SendsIdempotencyKeyHeaderAndBearerToken()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return JsonResponse(HttpStatusCode.OK, new { balance = 60m });
        });
        var client = CreateClient(handler);

        await client.DebitAsync(Guid.NewGuid(), 40m, "key-1", "ref-1", bearerToken: "test-token", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("key-1", capturedRequest!.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task DebitAsync_RetriesTransientFailures_AndSucceeds()
    {
        var attempt = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            attempt++;
            return attempt < 3
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : JsonResponse(HttpStatusCode.OK, new { balance = 60m });
        });

        var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(1));
        var client = CreateClient(handler, new PolicyHttpMessageHandler(retryPolicy));

        var result = await client.DebitAsync(
            Guid.NewGuid(), 40m, "key-1", "ref-1", bearerToken: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(60m, result.Value);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_ReturnsFailure_WhenRetriesAreExhausted()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(1));
        var client = CreateClient(handler, new PolicyHttpMessageHandler(retryPolicy));

        var result = await client.DebitAsync(
            Guid.NewGuid(), 40m, "key-1", "ref-1", bearerToken: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        // Original attempt + 3 retries.
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task DebitAsync_ReturnsCircuitBreakerFailureMessage_OnceTheCircuitOpens()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var breakerPolicy = HttpPolicyExtensions.HandleTransientHttpError().CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        var client = CreateClient(handler, new PolicyHttpMessageHandler(breakerPolicy));

        // Drive 5 consecutive failures to trip the breaker, then confirm the next
        // call short-circuits without hitting the handler at all.
        for (var i = 0; i < 5; i++)
            await client.DebitAsync(Guid.NewGuid(), 40m, $"key-{i}", "ref-1", bearerToken: null, CancellationToken.None);

        var callCountBeforeBreak = handler.CallCount;
        var result = await client.DebitAsync(Guid.NewGuid(), 40m, "key-final", "ref-1", bearerToken: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Wallet service unavailable - circuit breaker is open.", result.Error);
        Assert.Equal(callCountBeforeBreak, handler.CallCount);
    }
}
