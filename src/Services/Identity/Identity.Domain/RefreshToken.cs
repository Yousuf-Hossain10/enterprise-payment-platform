namespace Identity.Domain;

/// <summary>
/// Never store the raw refresh token - only its hash (docs/Security-Model.md §2).
/// ReplacedByTokenHash chains rotated tokens together for audit, per the tutorial's
/// rotation design (Phase 5): a used token is revoked, and the new token it was
/// replaced by is recorded here so token theft is detectable (Phase 5 DoD note).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public bool Revoked { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
