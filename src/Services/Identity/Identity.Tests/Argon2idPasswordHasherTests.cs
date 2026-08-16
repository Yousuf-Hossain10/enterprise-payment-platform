using Identity.Infrastructure;

namespace Identity.Tests;

public class Argon2idPasswordHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hasher = new Argon2idPasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new Argon2idPasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.False(hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentOutput_ForSamePasswordEachTime()
    {
        var hasher = new Argon2idPasswordHasher();

        var hash1 = hasher.Hash("same password");
        var hash2 = hasher.Hash("same password");

        Assert.NotEqual(hash1, hash2); // different random salt each call
        Assert.True(hasher.Verify("same password", hash1));
        Assert.True(hasher.Verify("same password", hash2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-enough-parts")]
    [InlineData("a.b.c.d.e.f")]
    [InlineData("notanumber.65536.2.c2FsdA==.aGFzaA==")]
    public void Verify_ReturnsFalse_ForMalformedHash(string malformedHash)
    {
        var hasher = new Argon2idPasswordHasher();

        Assert.False(hasher.Verify("any password", malformedHash));
    }
}
