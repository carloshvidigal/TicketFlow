using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Application.Auth;
using TicketFlow.Domain.Users;

namespace TicketFlow.Infrastructure.Auth;

// Access token JWT curto (15 min por padrão), assinado com HS256. Leva só o
// necessário para autorizar sem ir ao banco: quem é (sub), e-mail e papel.
public sealed class JwtAccessTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenGenerator
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Generate(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["email"] = user.Email,
                ["role"] = user.Role.ToString(),
                ["jti"] = Guid.NewGuid().ToString()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
