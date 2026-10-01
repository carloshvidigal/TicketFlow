using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Users;
using TicketFlow.Infrastructure.Database;

namespace TicketFlow.Infrastructure.Users;

public class UserRepository(AppDbContext dbContext) : IUserRepository
{
    private const string UniqueEmailIndex = "IX_users_Email";

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UniqueEmailIndex
        })
        {
            // Dois cadastros com o mesmo e-mail passaram juntos pela checagem
            // antecipada: quem barra de verdade é a unique constraint do banco.
            throw new EmailAlreadyRegisteredException();
        }
    }
}
