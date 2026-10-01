using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Application.Auth;
using TicketFlow.Domain.Users;
using TicketFlow.Infrastructure.Auth;
using TicketFlow.UnitTests.Support;

namespace TicketFlow.UnitTests.Auth;

public class JwtAccessTokenGeneratorTests
{
    private const string Secret = "um-segredo-de-teste-com-mais-de-32-bytes-0123456789";

    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly JwtAccessTokenGenerator _generator;
    private readonly User _user = new("carlos@example.com", "hash", UserRole.Organizer);

    public JwtAccessTokenGeneratorTests()
    {
        _generator = new JwtAccessTokenGenerator(Options.Create(new JwtOptions
        {
            Secret = Secret,
            Issuer = "TicketFlow",
            Audience = "TicketFlow-Clients",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        }), _clock);
    }

    [Fact]
    public void Generate_ExpiresFifteenMinutesAfterIssuing()
    {
        var token = _generator.Generate(_user);

        var expected = _clock.Now.UtcDateTime.AddMinutes(15);
        Assert.Equal(expected, token.ExpiresAt);
        Assert.Equal(expected, new JsonWebTokenHandler().ReadJsonWebToken(token.Value).ValidTo);
    }

    [Fact]
    public void Generate_CarriesUserIdEmailAndRoleAndTheConfiguredIssuerAndAudience()
    {
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(_generator.Generate(_user).Value);

        Assert.Equal(_user.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("carlos@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("Organizer", jwt.GetClaim("role").Value);
        Assert.Equal("TicketFlow", jwt.Issuer);
        Assert.Contains("TicketFlow-Clients", jwt.Audiences);
    }

    [Fact]
    public void Generate_NeverPutsThePasswordHashInTheToken()
    {
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(_generator.Generate(_user).Value);

        Assert.DoesNotContain(jwt.Claims, c => c.Value == _user.PasswordHash);
    }

    [Fact]
    public async Task Generate_IsSignedWithTheSecret_AndRejectedWithAnyOtherKey()
    {
        var token = _generator.Generate(_user).Value;

        var withRightKey = await Validate(token, Secret);
        var withWrongKey = await Validate(token, "outra-chave-completamente-diferente-0123456789");

        Assert.True(withRightKey.IsValid);
        Assert.False(withWrongKey.IsValid);
    }

    [Fact]
    public void Generate_ProducesADifferentTokenEachTime()
    {
        Assert.NotEqual(_generator.Generate(_user).Value, _generator.Generate(_user).Value);
    }

    // O relógio do teste está em 2026-01-01; a validação de vida útil é
    // desligada aqui porque o que se testa é só a assinatura.
    private static Task<TokenValidationResult> Validate(string token, string secret) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "TicketFlow",
            ValidAudience = "TicketFlow-Clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = false
        });
}
