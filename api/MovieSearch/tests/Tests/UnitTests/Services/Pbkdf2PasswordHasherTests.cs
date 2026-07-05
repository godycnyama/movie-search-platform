using Infrastructure.Services;

namespace MovieSearch.Tests.UnitTests.Services;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_ReturnsTrue_ForTheOriginalPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("correct horse battery staple", hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForAWrongPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("Tr0ub4dor&3", hash).ShouldBeFalse();
    }

    [Fact]
    public void Hash_UsesAUniqueSaltPerCall()
    {
        _hasher.Hash("same password").ShouldNotBe(_hasher.Hash("same password"));
    }

    [Fact]
    public void Hash_EmbedsAlgorithmIterationsSaltAndDigest()
    {
        var parts = _hasher.Hash("password").Split('.');

        parts.Length.ShouldBe(4);
        parts[0].ShouldBe("pbkdf2-sha256");
        int.Parse(parts[1]).ShouldBeGreaterThanOrEqualTo(600_000);
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("md5.1000.c2FsdA==.aGFzaA==")]           // unknown algorithm
    [InlineData("pbkdf2-sha256.abc.c2FsdA==.aGFzaA==")]   // non-numeric iterations
    public void Verify_ReturnsFalse_ForMalformedHashes(string malformed)
    {
        _hasher.Verify("password", malformed).ShouldBeFalse();
    }
}
