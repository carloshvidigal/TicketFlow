using TicketFlow.Application.Common;
using TicketFlow.Application.Users;

namespace TicketFlow.Application.Auth;

public record RefreshTokenCommand(string RefreshToken);

// Refresh token rotativo: cada token só vale uma vez. Ao usá-lo, ele é
// revogado e um novo par é emitido — tudo na mesma transação.
public class RefreshTokenHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    AuthTokenIssuer tokenIssuer,
    TimeProvider clock)
{
    public async Task<AuthTokens> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var stored = await refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(command.RefreshToken), cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        if (stored.IsRevoked)
        {
            // Um token já usado apareceu de novo: ou o cliente é bugado, ou
            // alguém copiou o token. Não dá para saber qual dos dois, então o
            // seguro é encerrar todas as sessões do usuário (exige novo login).
            await refreshTokens.RevokeAllActiveAsync(stored.UserId, now, cancellationToken);
            throw new InvalidRefreshTokenException();
        }

        if (stored.IsExpired(now))
            throw new InvalidRefreshTokenException();

        var user = await users.FindByIdAsync(stored.UserId, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        // Revogar o antigo e gravar o novo acontecem num único SaveChanges. Se
        // duas requisições tentarem rotacionar o mesmo token ao mesmo tempo, a
        // concorrência otimista deixa passar só uma; a outra é rejeitada.
        stored.Revoke(now);
        var tokens = tokenIssuer.Issue(user);

        try
        {
            await refreshTokens.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            throw new InvalidRefreshTokenException();
        }

        return tokens;
    }
}
