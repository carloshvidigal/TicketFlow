using Microsoft.Extensions.Logging;
using TicketFlow.Application.Common;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Users;

public record ChangeUserRoleCommand(Actor Actor, Guid UserId, UserRole Role);

// Gestão de papéis (ADR-0015). Pela API só se transita entre Customer e
// Organizer. Admin é um papel de altíssimo privilégio: nasce fora da API (banco)
// e a API nunca o concede nem o retira. Consequências: ninguém fica sem Admin
// por acidente, e um Admin comprometido não consegue criar outros.
//
// Quando a mudança vale: o papel viaja dentro do access token (15 min). O token
// já emitido continua com o papel antigo até expirar; a próxima renovação
// (/auth/refresh) e qualquer novo login releem o usuário no banco e já saem com o
// papel novo. Não há o que revogar: o access token não é revogável, e os refresh
// tokens não carregam papel algum.
public class ChangeUserRoleHandler(IUserRepository users, ILogger<ChangeUserRoleHandler> logger)
{
    public async Task<UserProfile> HandleAsync(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        UserAdministration.RequireAdmin(command.Actor);

        if (command.Role is not (UserRole.Customer or UserRole.Organizer))
            throw new DomainException("ROLE_NOT_ASSIGNABLE", "Only Customer and Organizer can be assigned through the API.");

        var user = await users.FindByIdForUpdateAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("USER_NOT_FOUND", "User not found.");

        // Cobre também o Admin tentando mudar o próprio papel.
        if (user.Role == UserRole.Admin)
            throw new DomainException("ADMIN_ROLE_NOT_MANAGEABLE", "The role of an Admin cannot be changed through the API.");

        var previous = user.Role;

        // Repetir o papel atual é um sucesso sem efeito (idempotente).
        if (previous == command.Role)
            return UserAdministration.ToProfile(user);

        user.ChangeRole(command.Role);
        await users.SaveChangesAsync(cancellationToken);

        // Trilha de auditoria mínima: quem mudou o papel de quem. Só ids, nunca e-mails.
        logger.LogInformation(
            "User {TargetUserId} role changed from {PreviousRole} to {NewRole} by admin {AdminUserId}",
            user.Id, previous, command.Role, command.Actor.UserId);

        return UserAdministration.ToProfile(user);
    }
}
