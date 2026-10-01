using TicketFlow.Application.Common;

namespace TicketFlow.Application.Auth;

// Mesma mensagem para e-mail inexistente e senha errada: a resposta não pode
// revelar qual dos dois falhou.
public class InvalidCredentialsException()
    : UnauthorizedException("INVALID_CREDENTIALS", "Invalid email or password.");

// Token inexistente, expirado, já usado ou revogado: para o cliente é tudo a
// mesma coisa — precisa fazer login de novo.
public class InvalidRefreshTokenException()
    : UnauthorizedException("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.");
