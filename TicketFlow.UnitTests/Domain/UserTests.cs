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

    [Fact]
    public void ChangePasswordHash_WithoutHash_ThrowsDomainException()
    {
        var user = new User("customer@ticketflow.dev", "old-hash", UserRole.Customer);

        var ex = Assert.Throws<DomainException>(() => user.ChangePasswordHash(" "));

        Assert.Equal("USER_PASSWORD_HASH_REQUIRED", ex.Code);
    }
}
