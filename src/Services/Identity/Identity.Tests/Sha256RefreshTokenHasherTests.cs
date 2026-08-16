using Identity.Infrastructure;

namespace Identity.Tests;

public class Sha256RefreshTokenHasherTests
{
    [Fact]
    public void Hash_IsDeterministic_ForSameInput()
    {
        var hasher = new Sha256RefreshTokenHasher();

        Assert.Equal(hasher.Hash("some-refresh-token"), hasher.Hash("some-refresh-token"));
    }

    [Fact]
    public void Hash_DiffersBetweenDifferentInputs()
    {
        var hasher = new Sha256RefreshTokenHasher();

        Assert.NotEqual(hasher.Hash("token-a"), hasher.Hash("token-b"));
    }
}
