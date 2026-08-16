using System.ComponentModel.DataAnnotations;

namespace Identity.Infrastructure;

public class TokenIssuanceOptions
{
    /// <summary>10-15 min per the tutorial's Phase 5 guidance.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>7-14 days per the tutorial's Phase 5 guidance.</summary>
    [Range(typeof(TimeSpan), "1.00:00:00", "30.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
}
