using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Common;
using TicketFlow.Domain.Auth;
using TicketFlow.Infrastructure.Database;

namespace TicketFlow.Infrastructure.Auth;

public class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => dbContext.RefreshTokens.Add(token);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrentUpdateException();
        }
    }

    // Operação em lote e idempotente, feita direto no banco: não há por que
    // carregar cada token na memória só para marcá-lo como revogado.
    public Task RevokeAllActiveAsync(Guid userId, DateTime now, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
}
