using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Security;

public class JwtOptions
{
    [Required]
    public string Issuer { get; set; } = default!;

    [Required]
    public string Audience { get; set; } = default!;

    /// <summary>
    /// HMAC-SHA256 signing key. At least 32 characters (256 bits) - shorter keys
    /// are weak against brute force. Never in appsettings.json outside local dev,
    /// per docs/Security-Model.md section 2.
    /// </summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = default!;
}
