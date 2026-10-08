using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TicketFlow.Api.Errors;

namespace TicketFlow.Api.RateLimiting;

// Seção "RateLimiting" da configuração.
public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    // Tentativas de login/cadastro/refresh por IP dentro da janela.
    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .Validate(o => o.AuthPermitLimit > 0 && o.AuthWindowSeconds > 0,
                "RateLimiting:AuthPermitLimit and RateLimiting:AuthWindowSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Limite por IP nos endpoints que aceitam credenciais: segura
            // tentativa-e-erro de senha e criação em massa de contas. Não
            // cobre um ataque distribuído contra uma única conta (muitos IPs);
            // nesse caso o custo do Argon2 é a barreira.
            limiter.AddPolicy(RateLimitPolicies.Auth, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(options.AuthWindowSeconds),
                        QueueLimit = 0
                    });
            });

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(new ErrorBody("RATE_LIMITED", "Too many requests. Please try again later.")),
                    cancellationToken);
            };
        });

        return services;
    }
}
