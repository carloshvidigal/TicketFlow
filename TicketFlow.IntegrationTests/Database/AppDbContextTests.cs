using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Users;

namespace TicketFlow.IntegrationTests.Database;

[Collection(DatabaseCollection.Name)]
public class AppDbContextTests(DatabaseFixture fixture)
{
    // Um evento precisa de um organizador que exista de verdade (FK).
    private async Task<User> AddOrganizerAsync()
    {
        var organizer = new User($"org-{Guid.NewGuid():N}@example.com", "hash", UserRole.Organizer);

        await using var context = fixture.CreateContext();
        context.Users.Add(organizer);
        await context.SaveChangesAsync();

        return organizer;
    }

    [Fact]
    public async Task CanPersistAndRetrieveEventWithSectionAndTickets()
    {
        var organizer = await AddOrganizerAsync();
        var @event = new Event(organizer.Id, "Rock Festival 2027", "Arena XYZ", DateTime.UtcNow.AddDays(30));
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

        var persistedEvent = await readContext.Events.SingleAsync(e => e.Id == @event.Id);
        var persistedSection = await readContext.Sections.SingleAsync(s => s.Id == section.Id);
        var persistedTickets = await readContext.Tickets.Where(t => t.SectionId == section.Id).ToListAsync();

        Assert.Equal("Arena XYZ", persistedEvent.Location);
        Assert.Equal(EventStatus.Draft, persistedEvent.Status);
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
        var organizer = await AddOrganizerAsync();
        var @event = new Event(organizer.Id, "Evento com FK protegida", "Arena XYZ", DateTime.UtcNow.AddDays(10));
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

    [Fact]
    public async Task AnEventCannotBeCreatedForAnOrganizerThatDoesNotExist()
    {
        var @event = new Event(Guid.NewGuid(), "Evento órfão", "Arena XYZ", DateTime.UtcNow.AddDays(10));

        await using var context = fixture.CreateContext();
        context.Events.Add(@event);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TwoSectionsOfTheSameEvent_CannotShareTheSameName()
    {
        var organizer = await AddOrganizerAsync();
        var @event = new Event(organizer.Id, "Evento", "Arena XYZ", DateTime.UtcNow.AddDays(10));

        await using (var setup = fixture.CreateContext())
        {
            setup.Events.Add(@event);
            await setup.SaveChangesAsync();
        }

        await using var context = fixture.CreateContext();
        context.Sections.Add(new Section(@event.Id, "Pista", 10, 100m));
        context.Sections.Add(new Section(@event.Id, "Pista", 20, 200m));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
