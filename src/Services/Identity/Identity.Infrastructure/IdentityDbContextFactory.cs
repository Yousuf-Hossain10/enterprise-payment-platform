using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` run against this project directly, without
/// spinning up the full Identity.Api host. Only used at migration-authoring
/// time - Identity.Api wires the real connection string via configuration
/// (ConnectionStrings:IdentityDb) at runtime. Defaults to the standard
/// `kubectl port-forward svc/postgres-postgresql 5432:5432` port; override
/// with IDENTITY_DB_CONNECTION if that port is unavailable locally.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=identity;Username=payment_platform;Password=local-dev-payment-platform";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
