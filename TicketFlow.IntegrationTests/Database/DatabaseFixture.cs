using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TicketFlow.Infrastructure.Database;

namespace TicketFlow.IntegrationTests.Database;

// Sobe um PostgreSQL real em container para os testes de integração,
// conforme decidido na stack (xUnit + Testcontainers), e a Api de verdade
// apontando para ele. Compartilhado entre os testes da mesma collection para
// não pagar o custo de subir container e host a cada teste.
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("ticketflow")
        .WithUsername("ticketflow")
        .WithPassword("ticketflow")
        .Build();

    public const string TestJwtSecret = "segredo-de-teste-de-integracao-com-mais-de-32-bytes";
    public const string TestIssuer = "TicketFlow";
    public const string TestAudience = "TicketFlow";

    private ApiFactory? _apiFactory;

    public HttpClient CreateApiClient() => _apiFactory!.CreateClient();

    public IServiceProvider ApiServices => _apiFactory!.Services;

    // Para testes que precisam de uma Api com configuração diferente da padrão.
    public WebApplicationFactory<Program> CreateApiFactory(
        string jwtSecret = TestJwtSecret,
        IReadOnlyDictionary<string, string>? settings = null) =>
        new ApiFactory(_container.GetConnectionString(), jwtSecret, settings);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        _apiFactory = new ApiFactory(_container.GetConnectionString(), TestJwtSecret, settings: null);
    }

    public async Task DisposeAsync()
    {
        if (_apiFactory is not null)
            await _apiFactory.DisposeAsync();

        await _container.DisposeAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class ApiFactory(
        string connectionString,
        string jwtSecret,
        IReadOnlyDictionary<string, string>? settings) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Jwt:Secret", jwtSecret);

            // Os testes compartilham um único IP (o do TestServer): o limite
            // real de produção só é exercitado nos testes de rate limiting.
            builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");

            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
                builder.UseSetting(key, value);
        }
    }
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database collection";
}
