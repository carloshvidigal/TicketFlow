using TicketFlow.Application.Auth;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Auth;

public class LoginHandlerTests
{
    private readonly AuthTestContext _ctx = new();

    [Fact]
    public async Task HandleAsync_WithValidCredentials_IssuesTokensAndStoresOnlyTheRefreshTokenHash()
    {
        var user = _ctx.AddUser();

        var tokens = await _ctx.LoginAsDefaultUserAsync();

        Assert.Equal($"access-token-for-{user.Id}", tokens.AccessToken);
        Assert.Equal(_ctx.Clock.Now.UtcDateTime.AddMinutes(15), tokens.AccessTokenExpiresAt);

        var stored = Assert.Single(_ctx.RefreshTokens.Tokens);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(RefreshTokenSecret.Hash(tokens.RefreshToken), stored.TokenHash);
        Assert.NotEqual(tokens.RefreshToken, stored.TokenHash);
        Assert.Equal(_ctx.Clock.Now.UtcDateTime.AddDays(7), stored.ExpiresAt);
        Assert.Equal(stored.ExpiresAt, tokens.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task HandleAsync_TreatsTheEmailAsCaseInsensitive()
    {
        _ctx.AddUser();

        var tokens = await _ctx.Login.HandleAsync(
            new LoginCommand("  CARLOS@Example.com ", AuthTestContext.DefaultPassword), CancellationToken.None);

        Assert.NotEmpty(tokens.AccessToken);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ThrowsInvalidCredentialsAndIssuesNothing()
    {
        _ctx.AddUser();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _ctx.Login.HandleAsync(new LoginCommand(AuthTestContext.DefaultEmail, "senha-errada-999"), CancellationToken.None));

        Assert.Empty(_ctx.RefreshTokens.Tokens);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownEmail_ThrowsTheSameErrorAsAWrongPassword()
    {
        var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _ctx.Login.HandleAsync(new LoginCommand("ninguem@example.com", "qualquer-senha-123"), CancellationToken.None));

        Assert.Equal("INVALID_CREDENTIALS", ex.Code);
    }

    // Anti-enumeração por tempo: e-mail inexistente também paga o custo de um hash.
    [Fact]
    public async Task HandleAsync_WithUnknownEmail_StillSpendsAHashToEqualizeResponseTime()
    {
        var before = _ctx.Hasher.HashCalls;

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _ctx.Login.HandleAsync(new LoginCommand("ninguem@example.com", "qualquer-senha-123"), CancellationToken.None));

        Assert.Equal(before + 1, _ctx.Hasher.HashCalls);
    }
}
