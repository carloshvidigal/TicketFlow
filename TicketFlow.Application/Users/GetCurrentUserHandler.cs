using TicketFlow.Application.Common;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Users;

public record UserProfile(Guid Id, string Email, UserRole Role);

public class GetCurrentUserHandler(IUserRepository users)
{
    public async Task<UserProfile> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Um JWT válido continua válido até expirar, mesmo que a conta tenha
        // sido removida nesse meio tempo. Por isso o perfil sempre sai do
        // banco, nunca das claims do token.
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("UNAUTHORIZED", "Authentication is required to access this resource.");

        return new UserProfile(user.Id, user.Email, user.Role);
    }
}
