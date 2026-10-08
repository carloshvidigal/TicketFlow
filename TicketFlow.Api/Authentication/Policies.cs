using Microsoft.AspNetCore.Authorization;
using TicketFlow.Domain.Users;

namespace TicketFlow.Api.Authentication;

public static class Policies
{
    public const string OrganizerOrAdmin = nameof(OrganizerOrAdmin);
    public const string AdminOnly = nameof(AdminOnly);

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Seguro por padrão (§3.4): toda rota exige usuário autenticado,
            // a menos que seja marcada explicitamente com [AllowAnonymous].
            // Esquecer um atributo, portanto, nunca expõe um endpoint sem querer.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(OrganizerOrAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(nameof(UserRole.Organizer), nameof(UserRole.Admin)))
            .AddPolicy(AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(nameof(UserRole.Admin)));

        return services;
    }
}
