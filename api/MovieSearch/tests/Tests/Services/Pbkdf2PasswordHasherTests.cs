using Infrastructure.Services;

namespace Tests.Services;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_ReturnsTrue_ForTheOriginalPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForAWrongPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("Tr0ub4dor&3", hash));
    }

    [Fact]
    public void Hash_UsesAUniqueSaltPerCall()
    {
        Assert.NotEqual(_hasher.Hash("same password"), _hasher.Hash("same password"));
    }

    [Fact]
    public void Hash_EmbedsAlgorithmIterationsSaltAndDigest()
    {
        var parts = _hasher.Hash("password").Split('.');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 600_000);
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("md5.1000.c2FsdA==.aGFzaA==")]           // unknown algorithm
    [InlineData("pbkdf2-sha256.abc.c2FsdA==.aGFzaA==")]   // non-numeric iterations
    public void Verify_ReturnsFalse_ForMalformedHashes(string malformed)
    {
        Assert.False(_hasher.Verify("password", malformed));
    }
}
