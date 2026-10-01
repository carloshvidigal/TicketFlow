using FluentValidation;

namespace TicketFlow.Api.Contracts.Auth;

public record LoginRequest(string Email, string Password);

// Sem regras de "força de senha" no login: quem decide se a senha serve é a
// verificação do hash. O limite máximo só protege o Argon2 de entradas gigantes.
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
