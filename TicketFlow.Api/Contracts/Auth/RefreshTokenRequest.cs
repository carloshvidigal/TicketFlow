using FluentValidation;

namespace TicketFlow.Api.Contracts.Auth;

// Usado tanto no refresh quanto no logout: nos dois casos o cliente apresenta
// o refresh token que recebeu no login.
public record RefreshTokenRequest(string RefreshToken);

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        // O token real tem 43 caracteres; o teto evita processar lixo gigante.
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
    }
}
