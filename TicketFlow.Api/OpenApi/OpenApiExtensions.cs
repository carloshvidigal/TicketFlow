using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TicketFlow.Api.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "TicketFlow API";
                document.Info.Description =
                    "API de venda e reserva de ingressos. Autentique-se em POST /auth/login e use o " +
                    "accessToken retornado no botão Authorize (esquema Bearer).";

                // Declara o esquema Bearer para o botão "Authorize" da
                // documentação interativa conseguir enviar o token.
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token JWT (15 min). Obtenha em POST /auth/login."
                };

                return Task.CompletedTask;
            });

            // Como a Api é "segura por padrão" (política de fallback exige
            // login), todo endpoint exige token, exceto os marcados como
            // [AllowAnonymous]. A documentação reflete exatamente isso.
            options.AddOperationTransformer((operation, context, _) =>
            {
                var requiresToken = !context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<IAllowAnonymous>().Any();

                if (requiresToken)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document)] = []
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
