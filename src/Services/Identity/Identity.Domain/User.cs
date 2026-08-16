namespace Identity.Domain;

/// <summary>
/// The aggregate root for authentication. Password hashing (Day 18) and token
/// issuance (Day 19) are Application-layer concerns; this is just the shape of
/// what's persisted, per docs/Microservice-Responsibilities.md.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string[] Roles { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
