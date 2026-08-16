namespace Identity.Application;

/// <summary>
/// Refresh tokens are never stored plain (docs/Security-Model.md §2). Separate from
/// IPasswordHasher: refresh tokens are already high-entropy random values, not
/// user-chosen secrets, so a fast deterministic hash is appropriate here - a slow,
/// memory-hard hash (Argon2id) would only add latency without adding security,
/// since there's no brute-forceable low-entropy input to protect against.
/// </summary>
public interface IRefreshTokenHasher
{
    string Hash(string refreshToken);
}
