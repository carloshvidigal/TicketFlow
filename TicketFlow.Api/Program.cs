using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TicketFlow.Api.Authentication;
using TicketFlow.Api.Contracts.Auth;
using TicketFlow.Api.Errors;
using TicketFlow.Api.OpenApi;
using TicketFlow.Api.RateLimiting;
using TicketFlow.Application;
using TicketFlow.Application.Auth;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create);

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. " +
        "Set it via environment variable (ConnectionStrings__DefaultConnection) or appsettings.Development.json.");

// O segredo do JWT vem de variável de ambiente (Jwt__Secret) e é validado no
// boot: sem ele (ou fraco demais), a Api nem sobe.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddJwtAuthentication();
builder.Services.AddAuthorizationPolicies();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

// Aplica migrations pendentes automaticamente só em Development (ex.: rodando
// via docker compose). Em produção isso vira um passo explícito de
// deploy/CI, não algo automático no boot (Fase 9, ainda não decidido).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

    // Documentação interativa da Api, só em desenvolvimento.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// Configure the HTTP request pipeline.

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseRateLimiter();

// A ordem importa: primeiro descobrir quem é o usuário (autenticação), depois
// decidir o que ele pode fazer (autorização).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Torna o Program acessível aos testes de integração (WebApplicationFactory).
public partial class Program;
