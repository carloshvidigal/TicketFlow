using TicketFlow.Application.Auth;
using TicketFlow.Domain.Users;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Auth;

public class RegisterUserHandlerTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(_users, new FakePasswordHasher());
    }

    [Fact]
    public async Task HandleAsync_CreatesACustomerWithHashedPasswordAndNormalizedEmail()
    {
        var result = await _handler.HandleAsync(
            new RegisterUserCommand("  Carlos@Example.com ", "uma-senha-forte-123"), CancellationToken.None);

        var saved = Assert.Single(_users.Saved);
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal("carlos@example.com", saved.Email);
        Assert.Equal(UserRole.Customer, saved.Role);
        Assert.Equal("hashed:uma-senha-forte-123", saved.PasswordHash);
        Assert.NotEqual("uma-senha-forte-123", saved.PasswordHash);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyRegistered_ThrowsAndSavesNothing()
    {
        await _handler.HandleAsync(new RegisterUserCommand("carlos@example.com", "uma-senha-forte-123"), CancellationToken.None);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            _handler.HandleAsync(new RegisterUserCommand("carlos@example.com", "outra-senha-456"), CancellationToken.None));

        Assert.Single(_users.Saved);
    }

    [Fact]
    public async Task HandleAsync_TreatsEmailsAsCaseInsensitive()
    {
        await _handler.HandleAsync(new RegisterUserCommand("carlos@example.com", "uma-senha-forte-123"), CancellationToken.None);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            _handler.HandleAsync(new RegisterUserCommand("CARLOS@EXAMPLE.COM", "outra-senha-456"), CancellationToken.None));
    }
}
