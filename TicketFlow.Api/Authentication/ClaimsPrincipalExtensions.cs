using System.Security.Claims;
using TicketFlow.Application.Common;
using TicketFlow.Domain.Users;

namespace TicketFlow.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    // O id do usuário vem do "sub" do JWT, já validado (assinatura, validade,
    // issuer e audience) pelo middleware de autenticação.
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var id) && id != Guid.Empty
            ? id
            : throw NotAuthenticated();

    // O papel vem da claim assinada do token. Consequência: uma mudança de
    // papel só vale para tokens emitidos depois dela (no máximo 15 minutos de
    // defasagem, a vida do access token — ver ADR-0011).
    public static Actor ToActor(this ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue("role");

        if (!Enum.TryParse<UserRole>(role, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
            throw NotAuthenticated();

        return new Actor(principal.GetUserId(), parsed);
    }

    // Para rotas públicas que se comportam diferente quando há alguém logado.
    public static Actor? ToActorOrNull(this ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true ? principal.ToActor() : null;

    private static UnauthorizedException NotAuthenticated() =>
        new("UNAUTHORIZED", "Authentication is required to access this resource.");
}
