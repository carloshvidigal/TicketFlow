using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Users;
using TicketFlow.IntegrationTests.Database;

namespace TicketFlow.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public class RegisterEndpointTests(DatabaseFixture fixture)
{
    private const string Password = "uma-senha-forte-123";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_WithValidData_CreatesACustomerAndNeverStoresThePlainPassword()
    {
        var client = fixture.CreateApiClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Customer", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));

        await using var db = fixture.CreateContext();
        var saved = await db.Users.SingleAsync(u => u.Email == email);
        Assert.StartsWith("$argon2id$", saved.PasswordHash);
        Assert.DoesNotContain(Password, saved.PasswordHash);
    }

    [Fact]
    public async Task Register_IgnoresAnyRoleSentByTheClient()
    {
        var client = fixture.CreateApiClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = Password, role = "Admin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var db = fixture.CreateContext();
        Assert.Equal(UserRole.Customer, (await db.Users.SingleAsync(u => u.Email == email)).Role);
    }

    [Fact]
    public async Task Register_WithAnEmailAlreadyRegistered_ReturnsConflictEvenWithDifferentCasing()
    {
        var client = fixture.CreateApiClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/auth/register", new { email, password = Password });

        var response = await client.PostAsJsonAsync("/auth/register", new { email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EMAIL_ALREADY_REGISTERED", body.GetProperty("error").GetProperty("code").GetString());
    }

    // Cenário concorrente: várias requisições com o mesmo e-mail chegam juntas,
    // passam pela checagem antecipada e só a unique constraint do banco decide.
    [Fact]
    public async Task Register_WithTheSameEmailInParallel_CreatesExactlyOneUser()
    {
        var client = fixture.CreateApiClient();
        var email = NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.PostAsJsonAsync("/auth/register", new { email, password = Password })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == email));
    }

    [Theory]
    [InlineData("not-an-email", Password, "email")]
    [InlineData("", Password, "email")]
    [InlineData("valid@example.com", "curta", "password")]
    [InlineData("valid@example.com", "", "password")]
    public async Task Register_WithInvalidData_ReturnsValidationErrorPointingAtTheField(string email, string password, string expectedField)
    {
        var client = fixture.CreateApiClient();

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.Contains(error.GetProperty("details").EnumerateArray(),
            detail => detail.GetProperty("field").GetString() == expectedField);
    }

    // A mensagem de um JSON inválido não pode vazar tipos nem posições internas.
    [Theory]
    [InlineData("{ \"email\": 123, \"password\": \"uma-senha-forte-123\" }", "email")]
    [InlineData("{ \"email\": \"a@b.com\", \"password\": [1, 2] }", "password")]
    [InlineData("{ \"email\": ", "")]
    public async Task Register_WithTheWrongJsonShape_DoesNotLeakInternalDetails(string json, string expectedField)
    {
        var client = fixture.CreateApiClient();

        var response = await client.PostAsync("/auth/register", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("TicketFlow", raw);
        Assert.DoesNotContain("System.", raw);
        Assert.DoesNotContain("LineNumber", raw);
        Assert.DoesNotContain("$.", raw);
        var error = JsonDocument.Parse(raw).RootElement.GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        if (expectedField != "")
            Assert.Contains(error.GetProperty("details").EnumerateArray(), d => d.GetProperty("field").GetString() == expectedField);
    }

    [Fact]
    public async Task Register_WithoutBody_ReturnsValidationErrorInTheStandardFormat()
    {
        var client = fixture.CreateApiClient();

        var response = await client.PostAsync("/auth/register",
            new StringContent("{ isso nao e json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
    }
}
