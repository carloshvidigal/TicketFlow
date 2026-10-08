using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.IntegrationTests.Database;

namespace TicketFlow.IntegrationTests.Support;

// Forja tokens "à mão" para provar que a Api rejeita o que não deve aceitar:
// expirado, assinado com outra chave, de outro issuer, sem assinatura...
public static class TestTokens
{
    public static string Create(
        Guid? userId = null,
        string role = "Customer",
        string secret = DatabaseFixture.TestJwtSecret,
        string issuer = DatabaseFixture.TestIssuer,
        string audience = DatabaseFixture.TestAudience,
        DateTime? expiresAt = null,
        bool signed = true,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTime.UtcNow;
        var expires = expiresAt ?? now.AddMinutes(15);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.AddHours(-3),
            NotBefore = now.AddHours(-3),
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = (userId ?? Guid.NewGuid()).ToString(),
                ["role"] = role
            },
            SigningCredentials = signed
                ? new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), algorithm)
                : null
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
