using System.Security.Cryptography;
using System.Text;
using Identity.Application;
using Konscious.Security.Cryptography;

namespace Identity.Infrastructure;

/// <summary>
/// Argon2id, per Security-Model.md/ADR-0004 (the formal ADR write-up lands Day 22 -
/// this is the implementation the sprint plan's Day 18 task names directly).
/// Parameters are stored alongside the hash so a future tuning change doesn't
/// invalidate already-issued hashes; verification uses a constant-time comparison
/// to avoid leaking timing information about how much of the hash matched.
/// </summary>
public class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 4;
    private const int MemorySizeKb = 65536; // 64 MB
    private const int DegreeOfParallelism = 2;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = ComputeHash(password, salt, Iterations, MemorySizeKb, DegreeOfParallelism);
        return string.Join('.',
            Iterations, MemorySizeKb, DegreeOfParallelism,
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hashedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 5)
            return false;

        if (!int.TryParse(parts[0], out var iterations) ||
            !int.TryParse(parts[1], out var memorySizeKb) ||
            !int.TryParse(parts[2], out var degreeOfParallelism))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[3]);
        var expectedHash = Convert.FromBase64String(parts[4]);

        var actualHash = ComputeHash(password, salt, iterations, memorySizeKb, degreeOfParallelism);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(
        string password, byte[] salt, int iterations, int memorySizeKb, int degreeOfParallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = iterations,
            MemorySize = memorySizeKb,
            DegreeOfParallelism = degreeOfParallelism
        };

        return argon2.GetBytes(HashSizeBytes);
    }
}
