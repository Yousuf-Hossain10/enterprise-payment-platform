using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Common;
using Payment.Application;
using Polly.CircuitBreaker;

namespace Payment.Infrastructure;

/// <summary>
/// HTTP adapter for IWalletClient. Resilience (retry, circuit breaker) is attached
/// to the HttpClient at registration time (Payment.Api/Program.cs), per
/// docs/Enterprise_Payment_Platform_Tutorial.md Phase 7's exact pattern - this class
/// only needs to translate HTTP responses into Result&lt;decimal&gt; and turn a broken
/// circuit into a clean failure instead of an unhandled exception.
///
/// The route/payload shape here (POST accounts/{id}/debit, Idempotency-Key header,
/// RFC 7807 problem+json on failure) is the contract Wallet's own debit endpoint is
/// expected to implement once it's built - Wallet.Api has no HTTP endpoints of its
/// own yet (see Wallet.Application/Debit.cs's note on the still-open caller-auth
/// decision), so this client is written against the documented API-Guidelines.md
/// conventions rather than a live endpoint, and is unit-tested against a fake
/// handler (Payment.Tests/WalletClientTests.cs) rather than a real Wallet instance.
/// </summary>
public class WalletClient : IWalletClient
{
    private readonly HttpClient _httpClient;

    public WalletClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<Result<decimal>> DebitAsync(
        Guid accountId, decimal amount, string idempotencyKey, string reference,
        string? bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/accounts/{accountId}/debit")
        {
            Content = JsonContent.Create(new DebitRequestBody(amount, reference))
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            return Result<decimal>.Failure("Wallet service unavailable - circuit breaker is open.");
        }
        catch (HttpRequestException ex)
        {
            return Result<decimal>.Failure($"Wallet service unreachable: {ex.Message}");
        }

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<DebitResponseBody>(cancellationToken);
            return Result<decimal>.Success(body!.Balance);
        }

        var fallbackMessage = $"Wallet debit failed with status {(int)response.StatusCode}.";

        // A failure response isn't guaranteed to carry a problem+json body (a bare
        // 500 from an intermediary proxy, for instance, won't) - ReadFromJsonAsync
        // throws on an empty or non-JSON body, so guard explicitly rather than let
        // that exception replace the real failure with a deserialization error.
        if (response.Content.Headers.ContentLength is null or 0)
            return Result<decimal>.Failure(fallbackMessage);

        ProblemDetailsBody? problem;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return Result<decimal>.Failure(fallbackMessage);
        }

        return Result<decimal>.Failure(problem?.Detail ?? fallbackMessage);
    }

    private record DebitRequestBody(decimal Amount, string Reference);
    private record DebitResponseBody(decimal Balance);
    private record ProblemDetailsBody(string? Title, string? Detail, int? Status);
}
