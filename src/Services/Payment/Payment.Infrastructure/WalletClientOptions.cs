using System.ComponentModel.DataAnnotations;

namespace Payment.Infrastructure;

public class WalletClientOptions
{
    [Required]
    [Url]
    public string BaseUrl { get; set; } = default!;
}
