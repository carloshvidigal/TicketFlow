using TicketFlow.Application.Auth;
using TicketFlow.Application.Common;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Auth;
using TicketFlow.Domain.Users;

namespace TicketFlow.UnitTests.Support;

// Fakes escritos à mão, em vez de uma lib de mock: o comportamento que
// importa (ex.: revogar em lote, falhar por concorrência) fica explícito.

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = now;

    public void Advance(TimeSpan by) => Now += by;

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class FakePasswordHasher : IPasswordHasher
{
    public int HashCalls { get; private set; }

    public string Hash(string password)
    {
        HashCalls++;
        return $"hashed:{password}";
    }

    public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
}

public sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Saved { get; } = [];

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.Any(u => u.Email == normalizedEmail));

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.SingleOrDefault(u => u.Email == normalizedEmail));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.SingleOrDefault(u => u.Id == id));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        Saved.Add(user);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _pending = [];

    public List<RefreshToken> Tokens { get; } = [];

    // Simula a concorrência otimista do banco: outra requisição gravou antes.
    public bool ThrowConcurrencyOnSave { get; set; }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.SingleOrDefault(t => t.TokenHash == tokenHash));

    public void Add(RefreshToken token) => _pending.Add(token);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ThrowConcurrencyOnSave)
        {
            _pending.Clear();
            throw new ConcurrentUpdateException();
        }

        Tokens.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }

    public Task RevokeAllActiveAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(t => t.UserId == userId && !t.IsRevoked))
            token.Revoke(now);

        return Task.CompletedTask;
    }
}

public sealed class FakeAccessTokenGenerator(TimeProvider clock) : IAccessTokenGenerator
{
    public AccessToken Generate(User user) =>
        new($"access-token-for-{user.Id}", clock.GetUtcNow().UtcDateTime.AddMinutes(15));
}
