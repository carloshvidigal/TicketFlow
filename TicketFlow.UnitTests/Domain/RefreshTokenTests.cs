using TicketFlow.Domain.Auth;
using TicketFlow.Domain.Common;

namespace TicketFlow.UnitTests.Domain;

public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefreshToken CreateToken() => new(Guid.NewGuid(), "hash", Now.AddDays(7), Now);

    [Fact]
    public void Constructor_WithoutUser_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() => new RefreshToken(Guid.Empty, "hash", Now.AddDays(7), Now));

        Assert.Equal("REFRESH_TOKEN_USER_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutHash_ThrowsDomainException(string? hash)
    {
        var ex = Assert.Throws<DomainException>(() => new RefreshToken(Guid.NewGuid(), hash!, Now.AddDays(7), Now));

        Assert.Equal("REFRESH_TOKEN_HASH_REQUIRED", ex.Code);
    }

    [Fact]
    public void Constructor_WithExpirationNotInTheFuture_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() => new RefreshToken(Guid.NewGuid(), "hash", Now, Now));

        Assert.Equal("REFRESH_TOKEN_EXPIRATION_INVALID", ex.Code);
    }

    [Fact]
    public void NewToken_IsActive()
    {
        var token = CreateToken();

        Assert.True(token.IsActive(Now));
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void IsExpired_IsTrueExactlyAtTheExpirationInstant()
    {
        var token = CreateToken();

        Assert.False(token.IsExpired(Now.AddDays(7).AddTicks(-1)));
        Assert.True(token.IsExpired(Now.AddDays(7)));
        Assert.False(token.IsActive(Now.AddDays(7)));
    }

    [Fact]
    public void Revoke_MarksTheTokenAsRevokedAndInactive()
    {
        var token = CreateToken();

        token.Revoke(Now.AddMinutes(1));

        Assert.True(token.IsRevoked);
        Assert.Equal(Now.AddMinutes(1), token.RevokedAt);
        Assert.False(token.IsActive(Now.AddMinutes(1)));
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsDomainException()
    {
        var token = CreateToken();
        token.Revoke(Now);

        var ex = Assert.Throws<DomainException>(() => token.Revoke(Now));

        Assert.Equal("REFRESH_TOKEN_ALREADY_REVOKED", ex.Code);
    }
}
