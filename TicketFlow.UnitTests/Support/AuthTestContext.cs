using Microsoft.Extensions.Options;
using TicketFlow.Application.Auth;
using TicketFlow.Domain.Users;

namespace TicketFlow.UnitTests.Support;

// Monta os handlers de autenticação com fakes em memória e um relógio que o
// teste controla — assim dá para "avançar o tempo" e provar a expiração.
public sealed class AuthTestContext
{
    public const string DefaultEmail = "carlos@example.com";
    public const string DefaultPassword = "uma-senha-forte-123";

    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public InMemoryUserRepository Users { get; } = new();
    public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();
    public FakePasswordHasher Hasher { get; } = new();

    public LoginHandler Login { get; }
    public RefreshTokenHandler Refresh { get; }
    public LogoutHandler Logout { get; }

    public AuthTestContext()
    {
        var options = Options.Create(new JwtOptions
        {
            Secret = new string('s', 48),
            Issuer = "TicketFlow",
            Audience = "TicketFlow",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

        var issuer = new AuthTokenIssuer(new FakeAccessTokenGenerator(Clock), RefreshTokens, options, Clock);

        Login = new LoginHandler(Users, Hasher, issuer, RefreshTokens);
        Refresh = new RefreshTokenHandler(RefreshTokens, Users, issuer, Clock);
        Logout = new LogoutHandler(RefreshTokens, Clock);
    }

    public User AddUser(string email = DefaultEmail, string password = DefaultPassword)
    {
        var user = new User(email, Hasher.Hash(password), UserRole.Customer);
        Users.Saved.Add(user);
        return user;
    }

    public Task<AuthTokens> LoginAsDefaultUserAsync() =>
        Login.HandleAsync(new LoginCommand(DefaultEmail, DefaultPassword), CancellationToken.None);
}
