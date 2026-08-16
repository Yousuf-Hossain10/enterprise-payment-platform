using System.Security.Cryptography;
using System.Text;
using Identity.Application;

namespace Identity.Infrastructure;

public class Sha256RefreshTokenHasher : IRefreshTokenHasher
{
    public string Hash(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
