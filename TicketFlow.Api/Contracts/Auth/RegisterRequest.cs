using FluentValidation;

namespace TicketFlow.Api.Contracts.Auth;

// Intencionalmente sem campo de papel: quem se cadastra é sempre Customer.
public record RegisterRequest(string Email, string Password);

public record RegisterResponse(Guid Id, string Email, string Role);

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();

        // O limite máximo protege o Argon2 (que é caro de propósito) de ser
        // usado para gastar CPU/memória com senhas gigantes.
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}
