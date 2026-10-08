using TicketFlow.Domain.Common;
using TicketFlow.Domain.Users;

namespace TicketFlow.UnitTests.Domain;

public class UserTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutEmail_ThrowsDomainException(string? email)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new User(email!, "hashed-password", UserRole.Customer));

        Assert.Equal("USER_EMAIL_REQUIRED", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithoutPasswordHash_ThrowsDomainException(string? passwordHash)
    {
        var ex = Assert.Throws<DomainException>(() =>
            new User("customer@ticketflow.dev", passwordHash!, UserRole.Customer));

        Assert.Equal("USER_PASSWORD_HASH_REQUIRED", ex.Code);
    }

    [Fact]
    public void Constructor_WithValidData_NormalizesEmail()
    {
        var user = new User("  Customer@TicketFlow.dev  ", "hashed-password", UserRole.Customer);

        Assert.Equal("customer@ticketflow.dev", user.Email);
        Assert.Equal(UserRole.Customer, user.Role);
    }

    [Fact]
    public void ChangePasswordHash_WithValidHash_Updates()
    {
        var user = new User("customer@ticketflow.dev", "old-hash", UserRole.Customer);

        user.ChangePasswordHash("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Theory]
    [InlineData(UserRole.Customer, UserRole.Organizer)]
    [InlineData(UserRole.Organizer, UserRole.Customer)]
    [InlineData(UserRole.Customer, UserRole.Admin)]
    public void ChangeRole_WithADefinedRole_UpdatesIt(UserRole from, UserRole to)
    {
        var user = new User("customer@ticketflow.dev", "hash", from);

        user.ChangeRole(to);

        Assert.Equal(to, user.Role);
    }

    [Fact]
    public void ChangeRole_WithAnUndefinedRole_ThrowsDomainExceptionAndKeepsTheOldRole()
    {
        var user = new User("customer@ticketflow.dev", "hash", UserRole.Customer);

        var ex = Assert.Throws<DomainException>(() => user.ChangeRole((UserRole)42));

        Assert.Equal("USER_ROLE_INVALID", ex.Code);
        Assert.Equal(UserRole.Customer, user.Role);
    }

    [Fact]
    public void ChangePasswordHash_WithoutHash_ThrowsDomainException()
    {
        var user = new User("customer@ticketflow.dev", "old-hash", UserRole.Customer);

        var ex = Assert.Throws<DomainException>(() => user.ChangePasswordHash(" "));

        Assert.Equal("USER_PASSWORD_HASH_REQUIRED", ex.Code);
    }
}
