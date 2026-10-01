using TicketFlow.Application.Auth;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Auth;

public class LogoutHandlerTests
{
    private readonly AuthTestContext _ctx = new();

    [Fact]
    public async Task HandleAsync_RevokesTheRefreshToken()
    {
        _ctx.AddUser();
        var tokens = await _ctx.LoginAsDefaultUserAsync();

        await _ctx.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken), CancellationToken.None);

        Assert.True(Assert.Single(_ctx.RefreshTokens.Tokens).IsRevoked);
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() =>
            _ctx.Refresh.HandleAsync(new RefreshTokenCommand(tokens.RefreshToken), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownToken_DoesNotThrow()
    {
        await _ctx.Logout.HandleAsync(new LogoutCommand("token-que-nunca-existiu"), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_CalledTwice_DoesNotThrow()
    {
        _ctx.AddUser();
        var tokens = await _ctx.LoginAsDefaultUserAsync();

        await _ctx.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken), CancellationToken.None);
        await _ctx.Logout.HandleAsync(new LogoutCommand(tokens.RefreshToken), CancellationToken.None);
    }
}
