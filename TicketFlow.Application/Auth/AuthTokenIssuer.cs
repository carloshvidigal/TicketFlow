using Microsoft.Extensions.Options;
using TicketFlow.Domain.Auth;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Auth;

public record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

// Emite o par access + refresh token de um usuário. Só faz Add() do refresh
// token: quem chama decide quando gravar, para poder juntar isso a outras
// alterações na mesma transação (ex.: revogar o token antigo no refresh).
public class AuthTokenIssuer(
    IAccessTokenGenerator accessTokens,
    IRefreshTokenRepository refreshTokens,
    IOptions<JwtOptions> options,
    TimeProvider clock)
{
    public AuthTokens Issue(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var accessToken = accessTokens.Generate(user);

        var rawRefreshToken = RefreshTokenSecret.Generate();
        var refreshToken = new RefreshToken(
            user.Id,
            RefreshTokenSecret.Hash(rawRefreshToken),
            now.AddDays(options.Value.RefreshTokenDays),
            now);

        refreshTokens.Add(refreshToken);

        return new AuthTokens(accessToken.Value, accessToken.ExpiresAt, rawRefreshToken, refreshToken.ExpiresAt);
    }
}
