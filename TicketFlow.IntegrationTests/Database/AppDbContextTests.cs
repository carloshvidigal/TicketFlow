using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Events;

namespace TicketFlow.IntegrationTests.Database;

[Collection(DatabaseCollection.Name)]
public class AppDbContextTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task CanPersistAndRetrieveEventWithSectionAndTickets()
    {
        var @event = new Event(Guid.NewGuid(), Guid.NewGuid(), "Rock Festival 2027", DateTime.UtcNow.AddDays(30));
        var section = new Section(@event.Id, "Pista", capacity: 3, price: 150m);
        var tickets = section.GenerateTickets();

        await using (var writeContext = fixture.CreateContext())
        {
            writeContext.Events.Add(@event);
            writeContext.Sections.Add(section);
            writeContext.Tickets.AddRange(tickets);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();

        var persistedSection = await readContext.Sections.SingleAsync(s => s.Id == section.Id);
        var persistedTickets = await readContext.Tickets.Where(t => t.SectionId == section.Id).ToListAsync();

        Assert.Equal(@event.Id, persistedSection.EventId);
        Assert.Equal(3, persistedTickets.Count);
        Assert.All(persistedTickets, t => Assert.Equal(TicketFlow.Domain.Tickets.TicketStatus.Available, t.Status));
    }

    // Prova de que a integridade referencial (§3.3 do documento) está de
    // fato garantida pelo banco, e não apenas pelo change tracker do EF: um
    // setor com tickets não pode ser apagado. Por isso o delete acontece num
    // DbContext novo, que nunca carregou os tickets — se ele carregasse, o
    // EF barraria a operação no cliente antes mesmo de chegar ao Postgres.
    [Fact]
    public async Task DeletingSectionWithTickets_IsRejectedByForeignKeyConstraint()
    {
        var @event = new Event(Guid.NewGuid(), Guid.NewGuid(), "Evento com FK protegida", DateTime.UtcNow.AddDays(10));
        var section = new Section(@event.Id, "Camarote", capacity: 1, price: 600m);
        var tickets = section.GenerateTickets();

        await using (var writeContext = fixture.CreateContext())
        {
            writeContext.Events.Add(@event);
            writeContext.Sections.Add(section);
            writeContext.Tickets.AddRange(tickets);
            await writeContext.SaveChangesAsync();
        }

        await using var deleteContext = fixture.CreateContext();
        var sectionToDelete = await deleteContext.Sections.SingleAsync(s => s.Id == section.Id);
        deleteContext.Sections.Remove(sectionToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }
}
