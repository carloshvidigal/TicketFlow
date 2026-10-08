using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Api.Errors;
using TicketFlow.Application.Auth;

namespace TicketFlow.Api.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configurado a partir do JwtOptions já validado no boot: segredo,
        // issuer e audience têm uma única fonte, a mesma usada para assinar.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Mantém os nomes das claims como estão no token ("sub", "role").
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    RequireSignedTokens = true,

                    // Só o algoritmo que a Api de fato usa para assinar: fecha a
                    // porta para ataques de troca de algoritmo.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    // O padrão é 5 minutos de tolerância, o que faria um token de
                    // 15 minutos valer 20. Aqui a tolerância é só para pequenos
                    // desvios de relógio.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = WriteUnauthorizedAsync,
                    OnForbidden = WriteForbiddenAsync
                };
            });

        return services;
    }

    // Sem isto, um 401/403 voltaria com corpo vazio, fora do formato de erro
    // único da Api (§17 do documento).
    private static async Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();

        // Um código próprio para token expirado permite ao cliente (a SPA)
        // saber que o caminho é renovar com o refresh token, não pedir login.
        var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
        var body = expired
            ? new ErrorBody("TOKEN_EXPIRED", "The access token has expired.")
            : new ErrorBody("UNAUTHORIZED", "Authentication is required to access this resource.");

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new ErrorResponse(body), context.HttpContext.RequestAborted);
    }

    private static async Task WriteForbiddenAsync(ForbiddenContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new ErrorResponse(new ErrorBody("FORBIDDEN", "You do not have permission to perform this action.")),
            context.HttpContext.RequestAborted);
    }
}
