using TicketFlow.Application.Common;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Users;

// Busca por e-mail EXATO, só para Admin: serve para achar o id de quem vai ter o
// papel alterado. Não há busca parcial nem listagem, para a rota não virar um
// jeito de despejar a base de usuários.
public class FindUserByEmailHandler(IUserRepository users)
{
    public async Task<UserProfile> HandleAsync(Actor actor, string email, CancellationToken cancellationToken)
    {
        UserAdministration.RequireAdmin(actor);

        var user = await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken)
            ?? throw new NotFoundException("USER_NOT_FOUND", "User not found.");

        return UserAdministration.ToProfile(user);
    }
}
