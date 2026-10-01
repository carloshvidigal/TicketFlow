using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Auth;

public record AccessToken(string Value, DateTime ExpiresAt);

// Porta: o formato concreto (JWT assinado com HS256) fica na Infrastructure.
public interface IAccessTokenGenerator
{
    AccessToken Generate(User user);
}
