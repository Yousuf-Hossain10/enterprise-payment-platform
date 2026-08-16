using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallet.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` run against this project directly, without
/// spinning up the full Wallet.Api host. Only used at migration-authoring time -
/// Wallet.Api wires the real connection string via configuration
/// (ConnectionStrings:WalletDb) at runtime. Defaults to the standard
/// `kubectl port-forward svc/postgres-postgresql 5432:5432` port; override
/// with WALLET_DB_CONNECTION if that port is unavailable locally.
/// </summary>
public class WalletDbContextFactory : IDesignTimeDbContextFactory<WalletDbContext>
{
    public WalletDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WALLET_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=wallet;Username=payment_platform;Password=local-dev-payment-platform";

        var optionsBuilder = new DbContextOptionsBuilder<WalletDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new WalletDbContext(optionsBuilder.Options);
    }
}
