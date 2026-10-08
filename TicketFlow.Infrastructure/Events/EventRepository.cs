using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Common;
using TicketFlow.Application.Events;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Database;

namespace TicketFlow.Infrastructure.Events;

public class EventRepository(AppDbContext dbContext) : IEventRepository
{
    public Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(Event @event) => dbContext.Events.Add(@event);

    public Task<int> CountSectionsAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.Sections.CountAsync(s => s.EventId == eventId, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrentUpdateException();
        }
    }

    public async Task AddSectionAsync(
        Guid eventId, Section section, IReadOnlyList<Ticket> tickets, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Trava a linha do evento até o fim da transação, e só se ele ainda for
        // rascunho. O UPDATE "sem efeito" faz três coisas ao mesmo tempo:
        //  1. confirma que o evento continua rascunho (0 linhas = não é mais);
        //  2. serializa criações de setor concorrentes no mesmo evento — a
        //     segunda espera a primeira terminar, então as checagens de limite e
        //     de nome abaixo enxergam o estado já confirmado;
        //  3. muda o xmin da linha, de modo que uma publicação que já tinha lido
        //     o evento (e contou os setores) falha na concorrência otimista em
        //     vez de publicar um evento cujo estoque acabou de mudar.
        var locked = await dbContext.Events
            .Where(e => e.Id == eventId && e.Status == EventStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EventStatus.Draft), cancellationToken);

        if (locked == 0)
            throw new DomainException("EVENT_NOT_EDITABLE", "The event can no longer be edited.");

        var existingNames = await dbContext.Sections
            .Where(s => s.EventId == eventId)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        if (existingNames.Count >= Event.MaxSections)
            throw new DomainException(
                "EVENT_SECTION_LIMIT_REACHED", $"An event can have at most {Event.MaxSections} sections.");

        if (existingNames.Any(name => string.Equals(name, section.Name, StringComparison.OrdinalIgnoreCase)))
            throw new SectionNameAlreadyExistsException();

        dbContext.Sections.Add(section);
        dbContext.Tickets.AddRange(tickets);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PagedResult<EventSummary>> ListPublishedAsync(
        DateTime now, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Events
            .AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.Date > now);

        var totalCount = await query.CountAsync(cancellationToken);

        // O desempate por Id mantém a ordem estável entre páginas quando dois
        // eventos têm a mesma data.
        var items = await query
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventSummary(e.Id, e.OrganizerId, e.Name, e.Location, e.Date, e.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<EventSummary>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<SectionAvailability>> GetSectionsAsync(
        Guid eventId, CancellationToken cancellationToken) =>
        await dbContext.Sections
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Price)
            .ThenBy(s => s.Name)
            .Select(s => new SectionAvailability(
                s.Id,
                s.Name,
                s.Capacity,
                s.Price,
                // Disponibilidade = tickets ainda Available; usa o índice (SectionId, Status).
                dbContext.Tickets.Count(t => t.SectionId == s.Id && t.Status == TicketStatus.Available)))
            .ToListAsync(cancellationToken);
}
