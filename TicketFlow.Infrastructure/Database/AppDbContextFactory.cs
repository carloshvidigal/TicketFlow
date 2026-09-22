using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TicketFlow.Infrastructure.Database;

// Usada apenas pela CLI do EF Core (dotnet ef migrations add / database update)
// para conseguir montar o modelo sem depender do host da Api. A connection
// string aqui nunca é usada para conectar de fato durante "migrations add" —
// só precisa ser sintaticamente válida para o provider Npgsql.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=ticketflow;Username=ticketflow;Password=design-time-only");

        return new AppDbContext(optionsBuilder.Options);
    }
}
