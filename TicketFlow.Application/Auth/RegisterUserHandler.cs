using TicketFlow.Application.Users;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Auth;

public record RegisterUserCommand(string Email, string Password);

public record RegisterUserResult(Guid Id, string Email, UserRole Role);

public class RegisterUserHandler(IUserRepository users, IPasswordHasher passwordHasher)
{
    public async Task<RegisterUserResult> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);

        // Checagem antecipada só para evitar o custo de um hash Argon2 à toa e
        // devolver o erro certo no caso comum. A garantia de verdade é a unique
        // constraint do banco, que o repositório traduz para a mesma exceção
        // quando dois cadastros com o mesmo e-mail chegam ao mesmo tempo.
        if (await users.EmailExistsAsync(email, cancellationToken))
            throw new EmailAlreadyRegisteredException();

        // Todo cadastro público nasce como Customer. O papel nunca vem do
        // cliente: Organizer/Admin serão concedidos por outro fluxo.
        var user = new User(email, passwordHasher.Hash(command.Password), UserRole.Customer);

        await users.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id, user.Email, user.Role);
    }
}
