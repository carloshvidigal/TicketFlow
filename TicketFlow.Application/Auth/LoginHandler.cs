using TicketFlow.Application.Users;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Auth;

public record LoginCommand(string Email, string Password);

public class LoginHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    AuthTokenIssuer tokenIssuer,
    IRefreshTokenRepository refreshTokens)
{
    public async Task<AuthTokens> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        if (user is null)
        {
            // Gasta o mesmo custo de um Argon2 que o caminho "usuário existe":
            // sem isso, o tempo de resposta denunciaria quais e-mails têm conta.
            passwordHasher.Hash(command.Password);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var tokens = tokenIssuer.Issue(user);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return tokens;
    }
}
