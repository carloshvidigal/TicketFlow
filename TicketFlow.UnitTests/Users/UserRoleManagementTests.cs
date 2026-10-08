using Microsoft.Extensions.Logging;
using TicketFlow.Application.Common;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Users;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Users;

public class UserRoleManagementTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly CapturingLogger<ChangeUserRoleHandler> _logger = new();
    private readonly ChangeUserRoleHandler _changeRole;
    private readonly FindUserByEmailHandler _findByEmail;

    private readonly Actor _admin = new(Guid.NewGuid(), UserRole.Admin);
    private readonly Actor _organizer = new(Guid.NewGuid(), UserRole.Organizer);
    private readonly Actor _customer = new(Guid.NewGuid(), UserRole.Customer);

    public UserRoleManagementTests()
    {
        _changeRole = new ChangeUserRoleHandler(_users, _logger);
        _findByEmail = new FindUserByEmailHandler(_users);
    }

    private User AddUser(UserRole role, string email = "alvo@example.com")
    {
        var user = new User(email, "hash", role);
        _users.Saved.Add(user);
        return user;
    }

    private Task<UserProfile> ChangeAsync(Actor actor, Guid userId, UserRole role) =>
        _changeRole.HandleAsync(new ChangeUserRoleCommand(actor, userId, role), CancellationToken.None);

    // ---- Mudança de papel --------------------------------------------------

    [Theory]
    [InlineData(UserRole.Customer, UserRole.Organizer)]
    [InlineData(UserRole.Organizer, UserRole.Customer)]
    public async Task Admin_ChangesBetweenCustomerAndOrganizer_AndPersistsIt(UserRole from, UserRole to)
    {
        var target = AddUser(from);

        var result = await ChangeAsync(_admin, target.Id, to);

        Assert.Equal(to, result.Role);
        Assert.Equal(to, target.Role);
        Assert.Equal(1, _users.SaveChangesCalls);
    }

    [Theory]
    [InlineData(UserRole.Organizer)]
    [InlineData(UserRole.Customer)]
    public async Task OnlyAnAdmin_CanChangeRoles(UserRole actorRole)
    {
        var target = AddUser(UserRole.Customer);
        var actor = actorRole == UserRole.Organizer ? _organizer : _customer;

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => ChangeAsync(actor, target.Id, UserRole.Organizer));

        Assert.Equal("FORBIDDEN", ex.Code);
        Assert.Equal(UserRole.Customer, target.Role);
        Assert.Equal(0, _users.SaveChangesCalls);
    }

    // Um Customer não consegue se promover: nem a si mesmo, nem a outro.
    [Fact]
    public async Task ACustomer_CannotPromoteThemselves()
    {
        var self = AddUser(UserRole.Customer);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ChangeAsync(new Actor(self.Id, UserRole.Customer), self.Id, UserRole.Organizer));

        Assert.Equal(UserRole.Customer, self.Role);
    }

    // A API nunca concede Admin: nem para um Customer, nem para um Organizer.
    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Organizer)]
    public async Task Admin_CannotBeGrantedThroughTheApi(UserRole currentRole)
    {
        var target = AddUser(currentRole);

        var ex = await Assert.ThrowsAsync<DomainException>(() => ChangeAsync(_admin, target.Id, UserRole.Admin));

        Assert.Equal("ROLE_NOT_ASSIGNABLE", ex.Code);
        Assert.Equal(currentRole, target.Role);
        Assert.Equal(0, _users.SaveChangesCalls);
    }

    // E a API nunca retira Admin de ninguém — inclusive de si mesmo.
    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Organizer)]
    public async Task TheRoleOfAnAdmin_CannotBeChangedThroughTheApi(UserRole newRole)
    {
        var otherAdmin = AddUser(UserRole.Admin, "outro.admin@example.com");

        var ex = await Assert.ThrowsAsync<DomainException>(() => ChangeAsync(_admin, otherAdmin.Id, newRole));

        Assert.Equal("ADMIN_ROLE_NOT_MANAGEABLE", ex.Code);
        Assert.Equal(UserRole.Admin, otherAdmin.Role);
    }

    [Fact]
    public async Task AnAdmin_CannotDemoteThemselves()
    {
        var self = AddUser(UserRole.Admin, "eu.admin@example.com");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            ChangeAsync(new Actor(self.Id, UserRole.Admin), self.Id, UserRole.Customer));

        Assert.Equal("ADMIN_ROLE_NOT_MANAGEABLE", ex.Code);
        Assert.Equal(UserRole.Admin, self.Role);
    }

    [Fact]
    public async Task ASecondAdmin_CannotBeCreatedByDemotingAndPromotingTricks()
    {
        // Mesmo encadeando operações, não existe caminho Customer -> Admin pela API.
        var target = AddUser(UserRole.Customer);
        await ChangeAsync(_admin, target.Id, UserRole.Organizer);

        await Assert.ThrowsAsync<DomainException>(() => ChangeAsync(_admin, target.Id, UserRole.Admin));

        Assert.Equal(UserRole.Organizer, target.Role);
    }

    [Fact]
    public async Task AnInvalidRoleValue_IsNotAssignable()
    {
        var target = AddUser(UserRole.Customer);

        await Assert.ThrowsAsync<DomainException>(() => ChangeAsync(_admin, target.Id, (UserRole)99));

        Assert.Equal(UserRole.Customer, target.Role);
    }

    [Fact]
    public async Task AnUnknownUser_IsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => ChangeAsync(_admin, Guid.NewGuid(), UserRole.Organizer));

        Assert.Equal("USER_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task SettingTheCurrentRole_IsASuccessWithNoEffect()
    {
        var target = AddUser(UserRole.Organizer);

        var result = await ChangeAsync(_admin, target.Id, UserRole.Organizer);

        Assert.Equal(UserRole.Organizer, result.Role);
        Assert.Equal(0, _users.SaveChangesCalls);
        Assert.Empty(_logger.Entries);
    }

    // ---- Auditoria ---------------------------------------------------------

    [Fact]
    public async Task AChange_IsAuditedWithWhoChangedWhoseRole_AndNeverLogsTheEmail()
    {
        var target = AddUser(UserRole.Customer, "pessoa.privada@example.com");

        await ChangeAsync(_admin, target.Id, UserRole.Organizer);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(target.Id, entry.State["TargetUserId"]);
        Assert.Equal(_admin.UserId, entry.State["AdminUserId"]);
        Assert.Equal(UserRole.Customer, entry.State["PreviousRole"]);
        Assert.Equal(UserRole.Organizer, entry.State["NewRole"]);
        Assert.DoesNotContain("pessoa.privada", entry.Message);
        Assert.DoesNotContain("example.com", entry.Message);
    }

    [Fact]
    public async Task ARejectedChange_LeavesNoAuditEntry()
    {
        var target = AddUser(UserRole.Customer);

        await Assert.ThrowsAsync<ForbiddenException>(() => ChangeAsync(_customer, target.Id, UserRole.Organizer));
        await Assert.ThrowsAsync<DomainException>(() => ChangeAsync(_admin, target.Id, UserRole.Admin));

        Assert.Empty(_logger.Entries);
    }

    // ---- Busca por e-mail ---------------------------------------------------

    [Fact]
    public async Task Admin_FindsAUserByExactEmail_IgnoringCaseAndSpaces()
    {
        var target = AddUser(UserRole.Customer, "alvo@example.com");

        var found = await _findByEmail.HandleAsync(_admin, "  ALVO@Example.com ", CancellationToken.None);

        Assert.Equal(target.Id, found.Id);
        Assert.Equal("alvo@example.com", found.Email);
        Assert.Equal(UserRole.Customer, found.Role);
    }

    [Fact]
    public async Task TheLookup_IsExactOnly_APartialEmailFindsNothing()
    {
        AddUser(UserRole.Customer, "alvo@example.com");

        await Assert.ThrowsAsync<NotFoundException>(() => _findByEmail.HandleAsync(_admin, "alvo", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _findByEmail.HandleAsync(_admin, "@example.com", CancellationToken.None));
    }

    [Theory]
    [InlineData(UserRole.Organizer)]
    [InlineData(UserRole.Customer)]
    public async Task OnlyAnAdmin_CanLookUpUsers(UserRole actorRole)
    {
        AddUser(UserRole.Customer, "alvo@example.com");
        var actor = actorRole == UserRole.Organizer ? _organizer : _customer;

        await Assert.ThrowsAsync<ForbiddenException>(() => _findByEmail.HandleAsync(actor, "alvo@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task LookingUpAnUnknownEmail_IsNotFound()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _findByEmail.HandleAsync(_admin, "ninguem@example.com", CancellationToken.None));

        Assert.Equal("USER_NOT_FOUND", ex.Code);
    }
}
