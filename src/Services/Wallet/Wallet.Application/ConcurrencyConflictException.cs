namespace Wallet.Application;

/// <summary>
/// Thrown by IAccountRepository.SaveChangesAsync when the Account's RowVersion
/// (Postgres xmin) no longer matches what was loaded - i.e. another debit/credit
/// committed first. Infrastructure translates the EF-specific
/// DbUpdateConcurrencyException into this so Application never takes a dependency
/// on EF Core (docs/Coding-Standards.md - Clean Architecture layering).
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
