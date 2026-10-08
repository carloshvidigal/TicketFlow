using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Users;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    // Devolve a entidade rastreada, para ser alterada e gravada por SaveRoleChangeAsync.
    Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    // Deve lançar EmailAlreadyRegisteredException se a unique constraint do
    // banco barrar um cadastro concorrente com o mesmo e-mail.
    Task AddAsync(User user, CancellationToken cancellationToken);

    // Grava a alteração feita no usuário rastreado (obtido por FindByIdForUpdateAsync).
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
