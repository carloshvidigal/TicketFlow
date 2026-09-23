using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Aplica migrations pendentes automaticamente só em Development (ex.: rodando
// via docker compose). Em produção isso vira um passo explícito de
// deploy/CI, não algo automático no boot (Fase 9, ainda não decidido).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
