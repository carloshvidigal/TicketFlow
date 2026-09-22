using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TicketFlow.Infrastructure.Database;

namespace TicketFlow.IntegrationTests.Database;

// Sobe um PostgreSQL real em container para os testes de integração,
// conforme decidido na stack (xUnit + Testcontainers). Compartilhado entre
// os testes da mesma collection para não pagar o custo de subir o container
// a cada teste.
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("ticketflow")
        .WithUsername("ticketflow")
        .WithPassword("ticketflow")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database collection";
}
