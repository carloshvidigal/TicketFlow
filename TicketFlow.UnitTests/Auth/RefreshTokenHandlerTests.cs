using TicketFlow.Application.Auth;
using TicketFlow.Domain.Auth;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Auth;

public class RefreshTokenHandlerTests
{
    private readonly AuthTestContext _ctx = new();

    private Task<AuthTokens> RefreshAsync(string refreshToken) =>
        _ctx.Refresh.HandleAsync(new RefreshTokenCommand(refreshToken), CancellationToken.None);

    private RefreshToken Stored(string rawToken) =>
        _ctx.RefreshTokens.Tokens.Single(t => t.TokenHash == RefreshTokenSecret.Hash(rawToken));

    [Fact]
    public async Task HandleAsync_WithAValidToken_RotatesIt()
    {
        _ctx.AddUser();
        var first = await _ctx.LoginAsDefaultUserAsync();

        var second = await RefreshAsync(first.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.True(Stored(first.RefreshToken).IsRevoked);
        Assert.True(Stored(second.RefreshToken).IsActive(_ctx.Clock.Now.UtcDateTime));
        Assert.Equal(2, _ctx.RefreshTokens.Tokens.Count);
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownToken_ThrowsInvalidRefreshToken()
    {
        var ex = await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => RefreshAsync("token-que-nunca-existiu"));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task HandleAsync_WithAnExpiredToken_ThrowsInvalidRefreshToken()
    {
        _ctx.AddUser();
        var tokens = await _ctx.LoginAsDefaultUserAsync();

        _ctx.Clock.Advance(TimeSpan.FromDays(7));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => RefreshAsync(tokens.RefreshToken));
        Assert.Single(_ctx.RefreshTokens.Tokens);
    }

    // Cenário de reuso: quem apresenta um token já rotacionado pode ser um
    // atacante com uma cópia — todas as sessões do usuário são encerradas.
    [Fact]
    public async Task HandleAsync_WithAnAlreadyUsedToken_RevokesEveryActiveTokenOfTheUser()
    {
        _ctx.AddUser();
        var first = await _ctx.LoginAsDefaultUserAsync();
        var second = await RefreshAsync(first.RefreshToken);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => RefreshAsync(first.RefreshToken));

        Assert.True(Stored(second.RefreshToken).IsRevoked);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => RefreshAsync(second.RefreshToken));
    }

    [Fact]
    public async Task HandleAsync_WhenAnotherRequestRotatedTheSameTokenFirst_RejectsThisOne()
    {
        _ctx.AddUser();
        var tokens = await _ctx.LoginAsDefaultUserAsync();
        _ctx.RefreshTokens.ThrowConcurrencyOnSave = true;

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => RefreshAsync(tokens.RefreshToken));
    }

    [Fact]
    public async Task HandleAsync_WhenTheUserNoLongerExists_ThrowsInvalidRefreshToken()
    {
        var user = _ctx.AddUser();
        var tokens = await _ctx.LoginAsDefaultUserAsync();
        _ctx.Users.Saved.Remove(user);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => RefreshAsync(tokens.RefreshToken));
    }
}
