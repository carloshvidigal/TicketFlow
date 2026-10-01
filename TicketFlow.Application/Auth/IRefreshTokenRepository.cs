using TicketFlow.Domain.Auth;

namespace TicketFlow.Application.Auth;

public interface IRefreshTokenRepository
{
    // Devolve a entidade rastreada: Revoke() + SaveChangesAsync() gravam a
    // alteração junto com qualquer Add() pendente, na mesma transação.
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken token);

    // Lança ConcurrentUpdateException se outra requisição alterou algum dos
    // registros rastreados no meio tempo (concorrência otimista).
    Task SaveChangesAsync(CancellationToken cancellationToken);

    // Revoga em lote todos os tokens ainda ativos do usuário (reuso detectado).
    Task RevokeAllActiveAsync(Guid userId, DateTime now, CancellationToken cancellationToken);
}
