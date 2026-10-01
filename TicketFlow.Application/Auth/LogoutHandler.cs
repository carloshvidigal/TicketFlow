using TicketFlow.Application.Common;

namespace TicketFlow.Application.Auth;

public record LogoutCommand(string RefreshToken);

public class LogoutHandler(IRefreshTokenRepository refreshTokens, TimeProvider clock)
{
    // Idempotente de propósito: token desconhecido, expirado ou já revogado
    // também "dá certo". Assim a resposta não revela se o token existia.
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        var stored = await refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(command.RefreshToken), cancellationToken);

        if (stored is null || stored.IsRevoked)
            return;

        stored.Revoke(clock.GetUtcNow().UtcDateTime);

        try
        {
            await refreshTokens.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            // Outra requisição revogou o mesmo token primeiro: o objetivo já foi atingido.
        }
    }
}
