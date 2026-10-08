using TicketFlow.Application.Common;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Users;

// Operações de administração de usuários são exclusivas do papel Admin. A Api
// já restringe a rota por política; esta checagem vale para qualquer outro
// ponto de entrada que chame os casos de uso.
internal static class UserAdministration
{
    public static void RequireAdmin(Actor actor)
    {
        if (actor.Role != UserRole.Admin)
            throw new ForbiddenException("FORBIDDEN", "You do not have permission to perform this action.");
    }

    public static UserProfile ToProfile(User user) => new(user.Id, user.Email, user.Role);
}
