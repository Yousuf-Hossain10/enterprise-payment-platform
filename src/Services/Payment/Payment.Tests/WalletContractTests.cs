using System.Net;
using System.Text;
using System.Text.Json;
using Payment.Infrastructure;

namespace Payment.Tests;

/// <summary>
/// Pins the wire contract between Payment (consumer) and Wallet (provider) as
/// documented in docs/API-Guidelines.md - route, headers, request/response JSON
/// shape. Wallet has no live debit endpoint yet (WalletClient.cs's own remarks), so
/// this is a consumer-driven contract test in the classic sense: it captures what
/// Payment actually requires from Wallet, expressed as assertions a fake handler can
/// verify today and a real Wallet implementation will need to satisfy once built.
/// Distinct from WalletClientTests.cs (Day 32), which covers resilience behavior
/// (retry/circuit-breaker) - this file is purely about wire-format correctness.
/// </summary>
public class WalletContractTests
{
    private class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        public RecordingHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder) => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request, CapturedBody);
        }
    }

    private static (WalletClient Client, RecordingHandler Handler) CreateClient(
        Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        var handler = new RecordingHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://wallet.contract-test") };
        return (new WalletClient(httpClient), handler);
    }

    [Fact]
    public async Task Debit_request_matches_the_documented_contract_method_route_and_headers()
    {
        var accountId = Guid.NewGuid();
        var (client, handler) = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"balance\": 60.00}", Encoding.UTF8, "application/json")
        });

        await client.DebitAsync(accountId, 40m, "idem-key-1", "ref-1", "bearer-token-1", CancellationToken.None);

        var request = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/v1/accounts/{accountId}/debit", request.RequestUri!.AbsolutePath);
        Assert.Equal("idem-key-1", request.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("bearer-token-1", request.Headers.Authorization.Parameter);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Debit_request_body_contains_exactly_the_documented_fields()
    {
        var (client, handler) = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"balance\": 60.00}", Encoding.UTF8, "application/json")
        });

        await client.DebitAsync(Guid.NewGuid(), 40.50m, "idem-key-1", "order-42", null, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        var root = body.RootElement;
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal(40.50m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("order-42", root.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task Debit_omits_the_Authorization_header_when_no_bearer_token_is_supplied()
    {
        var (client, handler) = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"balance\": 60.00}", Encoding.UTF8, "application/json")
        });

        await client.DebitAsync(Guid.NewGuid(), 40m, "idem-key-1", "ref-1", bearerToken: null, CancellationToken.None);

        Assert.Null(handler.CapturedRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task Debit_parses_the_documented_success_response_shape()
    {
        var (client, _) = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"balance\": 123.45}", Encoding.UTF8, "application/json")
        });

        var result = await client.DebitAsync(Guid.NewGuid(), 40m, "idem-key-1", "ref-1", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(123.45m, result.Value);
    }

    [Fact]
    public async Task Debit_parses_the_documented_RFC7807_problem_details_failure_shape()
    {
        // Exact shape from docs/API-Guidelines.md's own worked example, including
        // fields WalletClient doesn't currently use (type, traceId) - the contract
        // is "extra fields are tolerated", not "the shape must match exactly".
        const string problemJson = """
            {
              "type": "https://payment-platform.dev/errors/insufficient-balance",
              "title": "Insufficient balance",
              "status": 409,
              "detail": "Account 3f2a... has insufficient balance for a debit of 50.00 USD.",
              "instance": "/api/v1/accounts/3f2a.../debit",
              "traceId": "00-4bf92f...-00f067aa...-01"
            }
            """;
        var (client, _) = CreateClient((_, _) => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(problemJson, Encoding.UTF8, "application/problem+json")
        });

        var result = await client.DebitAsync(Guid.NewGuid(), 50m, "idem-key-1", "ref-1", null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Account 3f2a... has insufficient balance for a debit of 50.00 USD.", result.Error);
    }
}
