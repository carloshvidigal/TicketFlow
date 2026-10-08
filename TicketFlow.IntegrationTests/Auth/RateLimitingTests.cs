using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TicketFlow.IntegrationTests.Database;

namespace TicketFlow.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public class RateLimitingTests(DatabaseFixture fixture)
{
    // Janela longa de propósito: o teste não pode depender de a janela virar no meio.
    private WebApplicationFactoryWithLimit CreateLimitedApi(int permitLimit) => new(fixture, permitLimit);

    [Fact]
    public async Task Login_AfterTooManyAttempts_ReturnsTooManyRequestsWithRetryAfter()
    {
        using var api = CreateLimitedApi(permitLimit: 3);
        var client = api.CreateClient();
        var body = new { email = "ninguem@example.com", password = "qualquer-senha-123" };

        for (var attempt = 1; attempt <= 3; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login", body)).StatusCode);

        var blocked = await client.PostAsJsonAsync("/auth/login", body);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        var error = (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error");
        Assert.Equal("RATE_LIMITED", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheLimit_IsSharedAcrossLoginRegisterAndRefresh()
    {
        using var api = CreateLimitedApi(permitLimit: 3);
        var client = api.CreateClient();

        await client.PostAsJsonAsync("/auth/login", new { email = "a@example.com", password = "senha-qualquer-1" });
        await client.PostAsJsonAsync("/auth/register", new { email = "invalido", password = "x" });
        await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "token-qualquer" });

        var blocked = await client.PostAsJsonAsync("/auth/login", new { email = "a@example.com", password = "senha-qualquer-1" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task EndpointsThatDoNotTakeCredentials_AreNotAffectedByTheAuthLimit()
    {
        using var api = CreateLimitedApi(permitLimit: 1);
        var client = api.CreateClient();

        await client.PostAsJsonAsync("/auth/login", new { email = "a@example.com", password = "senha-qualquer-1" });
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/auth/login", new { email = "a@example.com", password = "senha-qualquer-1" })).StatusCode);

        // Logout e rotas autenticadas seguem funcionando para o mesmo IP.
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/auth/logout", new { refreshToken = "token-qualquer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/me")).StatusCode);
    }

    [Fact]
    public void TheApi_RefusesToStartWithAnInvalidRateLimitConfiguration()
    {
        using var api = fixture.CreateApiFactory(settings: new Dictionary<string, string>
        {
            ["RateLimiting:AuthPermitLimit"] = "0"
        });

        Assert.ThrowsAny<Exception>(() => api.CreateClient());
    }

    private sealed class WebApplicationFactoryWithLimit : IDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

        public WebApplicationFactoryWithLimit(DatabaseFixture fixture, int permitLimit)
        {
            _factory = fixture.CreateApiFactory(settings: new Dictionary<string, string>
            {
                ["RateLimiting:AuthPermitLimit"] = permitLimit.ToString(),
                ["RateLimiting:AuthWindowSeconds"] = "3600"
            });
        }

        public HttpClient CreateClient() => _factory.CreateClient();

        public void Dispose() => _factory.Dispose();
    }
}
