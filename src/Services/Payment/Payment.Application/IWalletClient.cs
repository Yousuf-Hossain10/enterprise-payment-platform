using BuildingBlocks.Common;

namespace Payment.Application;

/// <summary>
/// The Payment service's port to Wallet - Wallet's availability directly gates
/// whether the capture saga (Day 33) can proceed, so this interface is the seam
/// where retry/circuit-breaker resilience (docs/Microservice-Responsibilities.md)
/// gets applied, entirely in Infrastructure (WalletClient) so Application stays
/// free of any HTTP/Polly dependency.
/// </summary>
public interface IWalletClient
{
    /// <param name="bearerToken">
    /// Forwarded from the caller's own inbound request, if any - the saga (Day 33)
    /// supplies whatever token authorized the original capture request. Optional
    /// for now since Wallet has no protected endpoints of its own yet
    /// (docs/Security-Model.md #1 - the caller-auth model for internal calls is
    /// still an open decision); WalletClient sends it as a Bearer Authorization
    /// header when present so the client is ready for that decision without this
    /// service needing to make it today.
    /// </param>
    Task<Result<decimal>> DebitAsync(
        Guid accountId, decimal amount, string idempotencyKey, string reference,
        string? bearerToken, CancellationToken cancellationToken);
}
