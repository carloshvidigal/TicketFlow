using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using TicketFlow.Domain.Users;
using TicketFlow.IntegrationTests.Database;
using TicketFlow.IntegrationTests.Support;

namespace TicketFlow.IntegrationTests.Users;

[Collection(DatabaseCollection.Name)]
public class UserRoleManagementTests(DatabaseFixture fixture)
{
    private readonly HttpClient _anonymous = fixture.CreateApiClient();

    private Task<TestUser> NewUserAsync(UserRole role) => TestUsers.CreateAsync(fixture, role);

    private static Task<HttpResponseMessage> PutRoleAsync(HttpClient client, Guid userId, object body) =>
        client.PutAsJsonAsync($"/users/{userId}/role", body);

    private static Task<string> ErrorCodeAsync(HttpResponseMessage response) => EventsApi.ErrorCodeAsync(response);

    private async Task<UserRole> RoleInDatabaseAsync(Guid userId)
    {
        await using var db = fixture.CreateContext();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).Role;
    }

    // ---- Quem pode ---------------------------------------------------------

    [Fact]
    public async Task Admin_PromotesACustomerToOrganizer()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var customer = await NewUserAsync(UserRole.Customer);

        var response = await PutRoleAsync(admin.CreateClient(fixture), customer.Id, new { role = "Organizer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(customer.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Organizer", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.Equal(UserRole.Organizer, await RoleInDatabaseAsync(customer.Id));
    }

    // O ciclo completo que motivou a funcionalidade: ninguém mexe no banco.
    [Fact]
    public async Task APromotedCustomer_GetsTheNewRoleOnTheNextRefresh_AndCanThenCreateEvents()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var customer = await NewUserAsync(UserRole.Customer);
        var customerClient = customer.CreateClient(fixture);

        Assert.Equal(HttpStatusCode.Forbidden, (await EventsApi.CreateRawAsync(customerClient)).StatusCode);

        await PutRoleAsync(admin.CreateClient(fixture), customer.Id, new { role = "Organizer" });

        // O token que o usuário já tinha continua com o papel antigo (vale 15 min)...
        Assert.Equal(HttpStatusCode.Forbidden, (await EventsApi.CreateRawAsync(customerClient)).StatusCode);

        // ...e a renovação já traz o papel novo, sem precisar de novo login.
        var refreshed = await _anonymous.PostAsJsonAsync("/auth/refresh", new { refreshToken = customer.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var newToken = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        Assert.Equal("Organizer", new JsonWebTokenHandler().ReadJsonWebToken(newToken).GetClaim("role").Value);

        var organizerClient = fixture.CreateApiClient();
        organizerClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(HttpStatusCode.Created, (await EventsApi.CreateRawAsync(organizerClient)).StatusCode);
    }

    [Fact]
    public async Task ADemotedOrganizer_LosesTheAbilityToCreateEventsOnTheNextToken_ButKeepsTheirSession()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var organizer = await NewUserAsync(UserRole.Organizer);
        Assert.Equal(HttpStatusCode.Created, (await EventsApi.CreateRawAsync(organizer.CreateClient(fixture))).StatusCode);

        var response = await PutRoleAsync(admin.CreateClient(fixture), organizer.Id, new { role = "Customer" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // A sessão (refresh token) segue válida: rebaixar não desloga ninguém...
        var refreshed = await _anonymous.PostAsJsonAsync("/auth/refresh", new { refreshToken = organizer.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var newToken = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        // ...mas o token novo já é de Customer.
        Assert.Equal("Customer", new JsonWebTokenHandler().ReadJsonWebToken(newToken).GetClaim("role").Value);
        var client = fixture.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await EventsApi.CreateRawAsync(client)).StatusCode);
    }

    // Limite conhecido e documentado (ADR-0011/0015): o token já emitido vale até expirar.
    [Fact]
    public async Task TheOldAccessToken_KeepsTheOldRoleUntilItExpires()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var organizer = await NewUserAsync(UserRole.Organizer);

        await PutRoleAsync(admin.CreateClient(fixture), organizer.Id, new { role = "Customer" });

        Assert.Equal(HttpStatusCode.Created, (await EventsApi.CreateRawAsync(organizer.CreateClient(fixture))).StatusCode);
    }

    [Fact]
    public async Task TheRoleIsCaseInsensitive_AndOtherFieldsAreIgnored()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var customer = await NewUserAsync(UserRole.Customer);

        var response = await PutRoleAsync(admin.CreateClient(fixture), customer.Id,
            new { role = " organizer ", email = "outro@example.com", passwordHash = "hack", id = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext();
        var saved = await db.Users.AsNoTracking().SingleAsync(u => u.Id == customer.Id);
        Assert.Equal(UserRole.Organizer, saved.Role);
        Assert.Equal(customer.Email, saved.Email);
        Assert.StartsWith("$argon2id$", saved.PasswordHash);
    }

    [Fact]
    public async Task SettingTheCurrentRoleAgain_IsHarmless()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var organizer = await NewUserAsync(UserRole.Organizer);

        var response = await PutRoleAsync(admin.CreateClient(fixture), organizer.Id, new { role = "Organizer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserRole.Organizer, await RoleInDatabaseAsync(organizer.Id));
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Organizer)]
    public async Task NonAdmins_CannotChangeRoles_AndCannotPromoteThemselves(UserRole role)
    {
        var actor = await NewUserAsync(role);
        var target = await NewUserAsync(UserRole.Customer);

        var onOther = await PutRoleAsync(actor.CreateClient(fixture), target.Id, new { role = "Organizer" });
        var onSelf = await PutRoleAsync(actor.CreateClient(fixture), actor.Id, new { role = "Organizer" });

        Assert.Equal(HttpStatusCode.Forbidden, onOther.StatusCode);
        Assert.Equal("FORBIDDEN", await ErrorCodeAsync(onOther));
        Assert.Equal(HttpStatusCode.Forbidden, onSelf.StatusCode);
        Assert.Equal(UserRole.Customer, await RoleInDatabaseAsync(target.Id));
        Assert.Equal(role, await RoleInDatabaseAsync(actor.Id));
    }

    [Fact]
    public async Task WithoutAToken_TheAdminEndpointsReturn401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutRoleAsync(_anonymous, Guid.NewGuid(), new { role = "Organizer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/users?email=a@example.com")).StatusCode);
    }

    // ---- Admin é intocável pela API ----------------------------------------

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Organizer)]
    public async Task Admin_CannotBeGrantedThroughTheApi_ReturnsValidationError(UserRole currentRole)
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var target = await NewUserAsync(currentRole);

        var response = await PutRoleAsync(admin.CreateClient(fixture), target.Id, new { role = "Admin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ErrorCodeAsync(response));
        Assert.Equal(currentRole, await RoleInDatabaseAsync(target.Id));
    }

    [Fact]
    public async Task TheRoleOfAnotherAdmin_CannotBeChanged()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var otherAdmin = await NewUserAsync(UserRole.Admin);

        var response = await PutRoleAsync(admin.CreateClient(fixture), otherAdmin.Id, new { role = "Customer" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ADMIN_ROLE_NOT_MANAGEABLE", await ErrorCodeAsync(response));
        Assert.Equal(UserRole.Admin, await RoleInDatabaseAsync(otherAdmin.Id));
    }

    [Fact]
    public async Task AnAdmin_CannotDemoteThemselves_SoThereIsNeverAnAdminlessSystem()
    {
        var admin = await NewUserAsync(UserRole.Admin);

        var response = await PutRoleAsync(admin.CreateClient(fixture), admin.Id, new { role = "Customer" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ADMIN_ROLE_NOT_MANAGEABLE", await ErrorCodeAsync(response));
        Assert.Equal(UserRole.Admin, await RoleInDatabaseAsync(admin.Id));
    }

    // Dois admins se rebaixando ao mesmo tempo seriam a corrida clássica do
    // "último admin". Como a API não mexe em Admin, ela não existe: todas falham.
    [Fact]
    public async Task TwoAdminsTryingToDemoteEachOtherAtTheSameTime_BothFail_AndBothRemain()
    {
        var first = await NewUserAsync(UserRole.Admin);
        var second = await NewUserAsync(UserRole.Admin);

        var responses = await Task.WhenAll(
            PutRoleAsync(first.CreateClient(fixture), second.Id, new { role = "Customer" }),
            PutRoleAsync(second.CreateClient(fixture), first.Id, new { role = "Customer" }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
        Assert.Equal(UserRole.Admin, await RoleInDatabaseAsync(first.Id));
        Assert.Equal(UserRole.Admin, await RoleInDatabaseAsync(second.Id));
    }

    // ---- Validação ----------------------------------------------------------

    [Theory]
    [InlineData("Banana")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("Organizer,Admin")]
    public async Task AnInvalidRole_ReturnsValidationErrorPointingAtTheField(string role)
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var target = await NewUserAsync(UserRole.Customer);

        var response = await PutRoleAsync(admin.CreateClient(fixture), target.Id, new { role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("error").GetProperty("details").EnumerateArray();
        Assert.Contains(details, d => d.GetProperty("field").GetString() == "role");
        Assert.Equal(UserRole.Customer, await RoleInDatabaseAsync(target.Id));
    }

    [Fact]
    public async Task ARoleSentAsANumber_IsRejectedWithoutLeakingInternals()
    {
        var admin = await NewUserAsync(UserRole.Admin);
        var target = await NewUserAsync(UserRole.Customer);

        var response = await admin.CreateClient(fixture).PutAsync($"/users/{target.Id}/role",
            new StringContent("{ \"role\": 1 }", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("TicketFlow", await response.Content.ReadAsStringAsync());
        Assert.Equal(UserRole.Customer, await RoleInDatabaseAsync(target.Id));
    }

    [Fact]
    public async Task AnUnknownUser_Returns404_AndAMalformedIdReturns400()
    {
        var admin = (await NewUserAsync(UserRole.Admin)).CreateClient(fixture);

        var unknown = await PutRoleAsync(admin, Guid.NewGuid(), new { role = "Organizer" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("USER_NOT_FOUND", await ErrorCodeAsync(unknown));

        var malformed = await admin.PutAsJsonAsync("/users/isto-nao-e-um-guid/role", new { role = "Organizer" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    // ---- Busca por e-mail ----------------------------------------------------

    [Fact]
    public async Task Admin_FindsAUserByExactEmail_AndGetsNoSensitiveFields()
    {
        var admin = (await NewUserAsync(UserRole.Admin)).CreateClient(fixture);
        var target = await NewUserAsync(UserRole.Organizer);

        var response = await admin.GetAsync($"/users?email={Uri.EscapeDataString("  " + target.Email.ToUpperInvariant() + " ")}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(target.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(target.Email, body.GetProperty("email").GetString());
        Assert.Equal("Organizer", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task TheLookup_DoesNotDoPartialMatches_SoItCannotDumpTheUserBase()
    {
        var admin = (await NewUserAsync(UserRole.Admin)).CreateClient(fixture);
        await NewUserAsync(UserRole.Customer);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/users?email=user-")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/users?email=%40example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/users?email=%25")).StatusCode);
    }

    [Fact]
    public async Task TheLookup_RequiresAnEmail_AndRejectsNonAdmins()
    {
        var admin = (await NewUserAsync(UserRole.Admin)).CreateClient(fixture);
        var organizer = (await NewUserAsync(UserRole.Organizer)).CreateClient(fixture);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/users")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/users?email=")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await organizer.GetAsync("/users?email=a@example.com")).StatusCode);
    }

    [Fact]
    public async Task TheDocumentation_ListsTheAdminEndpoints_AsSecured()
    {
        var document = await (await _anonymous.GetAsync("/openapi/v1.json")).Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");

        Assert.True(paths.GetProperty("/users/{id}/role").GetProperty("put").TryGetProperty("security", out var security));
        Assert.True(security.GetArrayLength() > 0);
    }
}
