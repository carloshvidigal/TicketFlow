using FluentValidation;
using TicketFlow.Application.Users;

namespace TicketFlow.Api.Contracts.Users;

public record UserResponse(Guid Id, string Email, string Role)
{
    public static UserResponse From(UserProfile profile) => new(profile.Id, profile.Email, profile.Role.ToString());
}

public record UserLookupQuery(string? Email);

public class UserLookupQueryValidator : AbstractValidator<UserLookupQuery>
{
    public UserLookupQueryValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
    }
}

// O papel é recebido como texto: o desserializador de enum aceitaria números
// ("1") e valores fora do conjunto, e aqui só dois nomes são válidos.
public record ChangeRoleRequest(string Role);

public class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public static readonly string[] AssignableRoles = ["Customer", "Organizer"];

    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => role is not null && AssignableRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("'Role' must be Customer or Organizer.");
    }
}
