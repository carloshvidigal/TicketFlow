using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Domain.Users;
using TicketFlow.IntegrationTests.Database;
using TicketFlow.IntegrationTests.Support;

namespace TicketFlow.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public class AuthenticationTests(DatabaseFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateApiClient();

    private Task<HttpResponseMessage> GetMeAsync(string? token, string scheme = "Bearer")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);

        return _client.SendAsync(request);
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString()!;

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorizedInTheStandardErrorFormat()
    {
        var response = await GetMeAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ErrorCodeAsync(response));
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Me_WithAValidToken_ReturnsTheProfileFromTheDatabase_WithoutSensitiveFields()
    {
        var user = await TestUsers.CreateAsync(fixture, UserRole.Organizer);

        var response = await GetMeAsync(user.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.Equal("Organizer", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.False(body.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Me_WithAnExpiredToken_ReturnsTokenExpired_SoTheClientKnowsToRefresh()
    {
        var token = TestTokens.Create(expiresAt: DateTime.UtcNow.AddHours(-1));

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_EXPIRED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Me_WithATokenThatJustExpired_StillFailsWithinTheSmallClockSkew()
    {
        // Mais de 30 segundos atrás: fora da tolerância de relógio. O padrão do
        // framework (5 min) deixaria este token passar.
        var token = TestTokens.Create(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Me_WithATokenSignedWithAnotherKey_ReturnsUnauthorized()
    {
        var user = await TestUsers.CreateAsync(fixture);
        var forged = TestTokens.Create(user.Id, secret: "uma-chave-que-o-atacante-inventou-0123456789");

        var response = await GetMeAsync(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Me_WithAnUnsignedToken_AlgNone_ReturnsUnauthorized()
    {
        var user = await TestUsers.CreateAsync(fixture);
        var unsigned = TestTokens.Create(user.Id, signed: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(unsigned)).StatusCode);
    }

    // Só HS256 é aceito. Para o teste ser preciso, a Api sobe com uma chave longa
    // o bastante para HS512 existir: o MESMO segredo gera um token HS256 aceito
    // (controle) e um HS512 recusado, provando que o que barra é o algoritmo.
    [Fact]
    public async Task Me_WithATokenSignedWithAnAlgorithmTheApiDoesNotUse_ReturnsUnauthorized()
    {
        const string longSecret = "uma-chave-longa-o-bastante-para-hs512-com-mais-de-64-bytes-0123456789-abcdef";
        var user = await TestUsers.CreateAsync(fixture);
        using var api = fixture.CreateApiFactory(jwtSecret: longSecret);
        var client = api.CreateClient();

        async Task<HttpStatusCode> MeWith(string algorithm)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/me");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", TestTokens.Create(user.Id, secret: longSecret, algorithm: algorithm));
            return (await client.SendAsync(request)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await MeWith("HS256"));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeWith("HS512"));
    }

    [Theory]
    [InlineData("outro-issuer", DatabaseFixture.TestAudience)]
    [InlineData(DatabaseFixture.TestIssuer, "outra-audience")]
    public async Task Me_WithTheWrongIssuerOrAudience_ReturnsUnauthorized(string issuer, string audience)
    {
        var user = await TestUsers.CreateAsync(fixture);
        var token = TestTokens.Create(user.Id, issuer: issuer, audience: audience);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Me_WithAValidTokenForAUserThatNoLongerExists_ReturnsUnauthorized()
    {
        var token = TestTokens.Create(userId: Guid.NewGuid());

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHORIZED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Me_WithGarbageInTheBearerOrAnotherScheme_ReturnsUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync("isto-nao-e-um-jwt")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync("dXNlcjpwYXNz", scheme: "Basic")).StatusCode);
    }

    // O papel que vale é o da claim ASSINADA no token. Um cliente não consegue
    // se promover mandando outro dado: qualquer adulteração quebra a assinatura.
    [Fact]
    public async Task ATokenWithTamperedClaims_IsRejected()
    {
        var user = await TestUsers.CreateAsync(fixture);
        var parts = user.AccessToken.Split('.');
        var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1])));
        var tamperedPayload = payload.Replace("\"role\":\"Customer\"", "\"role\":\"Admin\"");
        Assert.NotEqual(payload, tamperedPayload);
        var tampered = $"{parts[0]}.{Base64Url(tamperedPayload)}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(tampered)).StatusCode);
    }

    // Guarda de regressão da política "seguro por padrão": a lista de rotas
    // públicas é fechada. Se alguém marcar um endpoint como [AllowAnonymous]
    // sem querer, este teste falha e obriga a decisão a ser consciente.
    [Fact]
    public void TheOnlyAnonymousEndpoints_AreTheOnesExplicitlyAllowed()
    {
        var endpoints = fixture.ApiServices.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e =>
            {
                var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
                return $"{string.Join(",", methods)} /{e.RoutePattern.RawText?.TrimStart('/')}";
            })
            .OrderBy(x => x)
            .ToList();

        // A documentação interativa (Scalar e OpenAPI) só existe em Development e
        // traz seus próprios assets estáticos; ela é conferida à parte, por prefixo.
        var application = endpoints
            .Where(e => !e.Contains(" /openapi") && !e.Contains(" /scalar"))
            .ToList();
        var documentation = endpoints.Except(application).ToList();

        var allowed = new[]
        {
            // autenticação
            "POST /auth/login",
            "POST /auth/logout",
            "POST /auth/refresh",
            "POST /auth/register",
            // leitura pública de eventos
            "GET /events",
            "GET /events/{id}"
        }.OrderBy(x => x);

        Assert.Equal(allowed, application);
        Assert.NotEmpty(documentation);
        Assert.All(documentation, e => Assert.StartsWith("GET ", e));
    }

    private static string Pad(string base64Url) =>
        base64Url.Replace('-', '+').Replace('_', '/').PadRight(base64Url.Length + (4 - base64Url.Length % 4) % 4, '=');

    private static string Base64Url(string text) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
