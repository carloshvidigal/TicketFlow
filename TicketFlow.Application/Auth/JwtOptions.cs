using System.Text;
using Microsoft.Extensions.Options;

namespace TicketFlow.Application.Auth;

// Seção "Jwt" da configuração. O Secret nunca é versionado: vem da variável
// de ambiente Jwt__Secret (§25 do documento).
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

// Roda na inicialização (ValidateOnStart): uma configuração insegura ou
// ausente derruba a Api no boot, em vez de falhar só no primeiro login.
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    // HS256 pede uma chave de pelo menos 256 bits.
    private const int MinSecretBytes = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (Encoding.UTF8.GetByteCount(options.Secret) < MinSecretBytes)
            errors.Add($"Jwt:Secret must be at least {MinSecretBytes} bytes long. " +
                       "Set it via the Jwt__Secret environment variable (see .env.example).");

        if (string.IsNullOrWhiteSpace(options.Issuer))
            errors.Add("Jwt:Issuer is required.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            errors.Add("Jwt:Audience is required.");

        if (options.AccessTokenMinutes <= 0)
            errors.Add("Jwt:AccessTokenMinutes must be greater than zero.");

        if (options.RefreshTokenDays <= 0)
            errors.Add("Jwt:RefreshTokenDays must be greater than zero.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
