using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Events;
using TicketFlow.Application.Users;

namespace TicketFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<ChangeUserRoleHandler>();
        services.AddScoped<FindUserByEmailHandler>();

        services.AddScoped<CreateEventHandler>();
        services.AddScoped<UpdateEventHandler>();
        services.AddScoped<EventLifecycleHandler>();
        services.AddScoped<AddSectionHandler>();
        services.AddScoped<EventQueries>();

        return services;
    }
}
