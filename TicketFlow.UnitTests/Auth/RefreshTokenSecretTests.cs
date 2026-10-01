using TicketFlow.Application.Auth;

namespace TicketFlow.UnitTests.Auth;

public class RefreshTokenSecretTests
{
    [Fact]
    public void Generate_ProducesUniqueUrlSafeTokensWith256BitsOfEntropy()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenSecret.Generate()).ToList();

        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, t =>
        {
            Assert.Equal(43, t.Length);
            Assert.DoesNotContain('+', t);
            Assert.DoesNotContain('/', t);
            Assert.DoesNotContain('=', t);
        });
    }

    [Fact]
    public void Hash_IsDeterministicHexAndDoesNotContainTheToken()
    {
        var token = RefreshTokenSecret.Generate();

        var hash = RefreshTokenSecret.Hash(token);

        Assert.Equal(hash, RefreshTokenSecret.Hash(token));
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.NotEqual(hash, RefreshTokenSecret.Hash(RefreshTokenSecret.Generate()));
    }
}
