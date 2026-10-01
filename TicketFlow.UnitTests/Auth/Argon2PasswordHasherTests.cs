using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.UnitTests.Auth;

public class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesArgon2idHashThatDoesNotContainThePassword()
    {
        var hash = _hasher.Hash("uma-senha-forte-123");

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash);
        Assert.DoesNotContain("uma-senha-forte-123", hash);
    }

    [Fact]
    public void Hash_UsesARandomSaltEachTime()
    {
        var first = _hasher.Hash("uma-senha-forte-123");
        var second = _hasher.Hash("uma-senha-forte-123");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("uma-senha-forte-123");

        Assert.True(_hasher.Verify("uma-senha-forte-123", hash));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("uma-senha-forte-123");

        Assert.False(_hasher.Verify("outra-senha-456", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$salt-sem-hash")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=abc,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$!!!$@@@")]
    public void Verify_WithMalformedHash_ReturnsFalseInsteadOfThrowing(string malformedHash)
    {
        Assert.False(_hasher.Verify("uma-senha-forte-123", malformedHash));
    }
}
