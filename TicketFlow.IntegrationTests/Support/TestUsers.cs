using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Users;
using TicketFlow.IntegrationTests.Database;

namespace TicketFlow.IntegrationTests.Support;

public sealed record TestUser(Guid Id, string Email, UserRole Role, string AccessToken, string RefreshToken)
{
    // Cliente HTTP já autenticado como este usuário.
    public HttpClient CreateClient(DatabaseFixture fixture)
    {
        var client = fixture.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }
}

public static class TestUsers
{
    public const string Password = "uma-senha-forte-123";

    // Cria o usuário pelo fluxo real da Api (registro + login). Como o cadastro
    // público só gera Customer, o papel é promovido direto no banco antes do
    // login, para o papel já constar no access token.
    public static async Task<TestUser> CreateAsync(DatabaseFixture fixture, UserRole role = UserRole.Customer)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var client = fixture.CreateApiClient();

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        if (register.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"Register failed: {register.StatusCode}");

        var id = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        if (role != UserRole.Customer)
        {
            await using var db = fixture.CreateContext();
            await db.Users.Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, role));
        }

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        if (login.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Login failed: {login.StatusCode}");

        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();

        return new TestUser(
            id,
            email,
            role,
            tokens.GetProperty("accessToken").GetString()!,
            tokens.GetProperty("refreshToken").GetString()!);
    }
}
