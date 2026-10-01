using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using TicketFlow.Application.Auth;
using TicketFlow.IntegrationTests.Database;

namespace TicketFlow.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public class TokenEndpointsTests(DatabaseFixture fixture)
{
    private const string Password = "uma-senha-forte-123";

    private readonly HttpClient _client = fixture.CreateApiClient();

    private async Task<string> RegisterAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password = Password) =>
        _client.PostAsJsonAsync("/auth/login", new { email, password });

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

    private async Task<JsonElement> LoginOkAsync(string email)
    {
        var response = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString()!;

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAFifteenMinuteAccessTokenAndStoresOnlyTheRefreshTokenHash()
    {
        var email = await RegisterAsync();

        var body = await LoginOkAsync(email);

        // Access token: JWT com sub/role do usuário e 15 minutos de vida.
        await using var db = fixture.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("accessToken").GetString()!);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("Customer", jwt.GetClaim("role").Value);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.ValidFrom);

        // Refresh token: no banco só existe o hash, nunca o valor entregue ao cliente.
        var rawRefreshToken = body.GetProperty("refreshToken").GetString()!;
        var stored = await db.RefreshTokens.SingleAsync(t => t.UserId == user.Id);
        Assert.Equal(RefreshTokenSecret.Hash(rawRefreshToken), stored.TokenHash);
        Assert.DoesNotContain(rawRefreshToken, stored.TokenHash);
        Assert.Null(stored.RevokedAt);
        Assert.InRange(stored.ExpiresAt, DateTime.UtcNow.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_ReturnsIdenticalUnauthorizedResponses()
    {
        var email = await RegisterAsync();

        var wrongPassword = await LoginAsync(email, "senha-errada-999");
        var unknownEmail = await LoginAsync($"ninguem-{Guid.NewGuid():N}@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await ErrorCodeAsync(wrongPassword));
        // O corpo é idêntico: a resposta não revela qual dos dois falhou.
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownEmail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithMissingFields_ReturnsValidationError()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Refresh_RotatesTheToken_AndTheOldOneStopsWorking()
    {
        var first = await LoginOkAsync(await RegisterAsync());
        var firstRefreshToken = first.GetProperty("refreshToken").GetString()!;

        var response = await RefreshAsync(firstRefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(firstRefreshToken, second.GetProperty("refreshToken").GetString());
        Assert.False(string.IsNullOrEmpty(second.GetProperty("accessToken").GetString()));

        var reuse = await RefreshAsync(firstRefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("INVALID_REFRESH_TOKEN", await ErrorCodeAsync(reuse));
    }

    [Fact]
    public async Task Refresh_WithAnAlreadyUsedToken_EndsTheWholeSession()
    {
        var email = await RegisterAsync();
        var first = await LoginOkAsync(email);
        var firstRefreshToken = first.GetProperty("refreshToken").GetString()!;
        var second = await (await RefreshAsync(firstRefreshToken)).Content.ReadFromJsonAsync<JsonElement>();
        var secondRefreshToken = second.GetProperty("refreshToken").GetString()!;

        // Reuso do token antigo: alguém pode ter copiado o token.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(firstRefreshToken)).StatusCode);

        // O token novo, que era legítimo, também deixa de valer.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(secondRefreshToken)).StatusCode);

        await using var db = fixture.CreateContext();
        var userId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
        Assert.Empty(await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync());
    }

    // Corrida: várias requisições tentam rotacionar o MESMO refresh token ao
    // mesmo tempo. A concorrência otimista (xmin) deixa passar exatamente uma.
    [Fact]
    public async Task Refresh_WithTheSameTokenInParallel_SucceedsForExactlyOneRequest()
    {
        var login = await LoginOkAsync(await RegisterAsync());
        var refreshToken = login.GetProperty("refreshToken").GetString()!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => RefreshAsync(refreshToken)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Refresh_WithAnExpiredToken_ReturnsUnauthorized()
    {
        var email = await RegisterAsync();
        var login = await LoginOkAsync(email);

        await using (var db = fixture.CreateContext())
        {
            var userId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
            await db.RefreshTokens.Where(t => t.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        var response = await RefreshAsync(login.GetProperty("refreshToken").GetString()!);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_REFRESH_TOKEN", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_ReturnsUnauthorized_AndWithoutOneReturnsValidationError()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("token-que-nunca-existiu")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync("")).StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var login = await LoginOkAsync(await RegisterAsync());
        var refreshToken = login.GetProperty("refreshToken").GetString()!;

        var logout = await _client.PostAsJsonAsync("/auth/logout", new { refreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(refreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_WithAnUnknownTokenOrCalledTwice_StillReturnsNoContent()
    {
        var login = await LoginOkAsync(await RegisterAsync());
        var refreshToken = login.GetProperty("refreshToken").GetString()!;

        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("/auth/logout", new { refreshToken = "token-que-nunca-existiu" })).StatusCode);

        await _client.PostAsJsonAsync("/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("/auth/logout", new { refreshToken })).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curto")]
    public void TheApi_RefusesToStartWithoutAStrongJwtSecret(string secret)
    {
        using var factory = fixture.CreateApiFactory(secret);

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt__Secret", ExceptionMessages(ex));
    }

    private static string ExceptionMessages(Exception? ex) =>
        ex is null ? "" : ex.Message + " " + ExceptionMessages(ex.InnerException);
}
