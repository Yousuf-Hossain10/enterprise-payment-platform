using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Payment.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` run against this project directly, without
/// spinning up the full Payment.Api host. Only used at migration-authoring time -
/// Payment.Api wires the real connection string via configuration
/// (ConnectionStrings:PaymentDb) at runtime. Defaults to the standard
/// `kubectl port-forward svc/postgres-postgresql 5432:5432` port; override
/// with PAYMENT_DB_CONNECTION if that port is unavailable locally.
/// </summary>
public class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PAYMENT_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=payment;Username=payment_platform;Password=local-dev-payment-platform";

        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PaymentDbContext(optionsBuilder.Options);
    }
}
