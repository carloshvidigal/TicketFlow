using TicketFlow.Application.Common;
using TicketFlow.Application.Events;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Tickets;
using TicketFlow.Domain.Users;

namespace TicketFlow.UnitTests.Support;

// Repositório em memória só para os testes dos casos de uso. As garantias que
// dependem do banco (transação, trava, unique index) são provadas nos testes
// de integração, contra o Postgres de verdade.
public sealed class InMemoryEventRepository : IEventRepository
{
    private readonly List<Event> _pending = [];

    public List<Event> Events { get; } = [];
    public List<Section> Sections { get; } = [];
    public List<Ticket> Tickets { get; } = [];

    public bool ThrowConcurrencyOnSave { get; set; }
    public DateTime? LastListNow { get; private set; }
    public (int Page, int PageSize)? LastListPaging { get; private set; }

    public Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Events.SingleOrDefault(e => e.Id == id));

    public void Add(Event @event) => _pending.Add(@event);

    public Task<int> CountSectionsAsync(Guid eventId, CancellationToken cancellationToken) =>
        Task.FromResult(Sections.Count(s => s.EventId == eventId));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ThrowConcurrencyOnSave)
            throw new ConcurrentUpdateException();

        Events.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }

    public Task AddSectionAsync(
        Guid eventId, Section section, IReadOnlyList<Ticket> tickets, CancellationToken cancellationToken)
    {
        Sections.Add(section);
        Tickets.AddRange(tickets);
        return Task.CompletedTask;
    }

    public Task<PagedResult<EventSummary>> ListPublishedAsync(
        DateTime now, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastListNow = now;
        LastListPaging = (page, pageSize);

        var all = Events.Where(e => e.Status == EventStatus.Published && e.Date > now).OrderBy(e => e.Date).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EventSummary(e.Id, e.OrganizerId, e.Name, e.Location, e.Date, e.Status))
            .ToList();

        return Task.FromResult(new PagedResult<EventSummary>(items, page, pageSize, all.Count));
    }

    public Task<IReadOnlyList<SectionAvailability>> GetSectionsAsync(Guid eventId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SectionAvailability>>(Sections
            .Where(s => s.EventId == eventId)
            .Select(s => new SectionAvailability(
                s.Id, s.Name, s.Capacity, s.Price,
                Tickets.Count(t => t.SectionId == s.Id && t.Status == TicketStatus.Available)))
            .ToList());
}

// Os cinco tipos de pessoa que importam para as regras de acesso a eventos.
public sealed class EventTestContext
{
    public InMemoryEventRepository Repository { get; } = new();
    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public Actor Owner { get; } = new(Guid.NewGuid(), UserRole.Organizer);
    public Actor OtherOrganizer { get; } = new(Guid.NewGuid(), UserRole.Organizer);
    public Actor Admin { get; } = new(Guid.NewGuid(), UserRole.Admin);
    public Actor Customer { get; } = new(Guid.NewGuid(), UserRole.Customer);

    public CreateEventHandler CreateEvent { get; }
    public UpdateEventHandler UpdateEvent { get; }
    public EventLifecycleHandler Lifecycle { get; }
    public AddSectionHandler AddSection { get; }
    public EventQueries Queries { get; }

    public EventTestContext()
    {
        CreateEvent = new CreateEventHandler(Repository);
        UpdateEvent = new UpdateEventHandler(Repository);
        Lifecycle = new EventLifecycleHandler(Repository);
        AddSection = new AddSectionHandler(Repository);
        Queries = new EventQueries(Repository, Clock);
    }

    public static DateTime InTheFuture => DateTime.UtcNow.AddDays(30);

    // Cria um evento do Owner já no status pedido (com um setor, se for publicado).
    public Event AddEvent(EventStatus status = EventStatus.Draft, Actor? organizer = null)
    {
        var @event = new Event((organizer ?? Owner).UserId, "Rock Festival 2027", "Arena XYZ", InTheFuture);
        Repository.Events.Add(@event);

        if (status != EventStatus.Draft && status != EventStatus.Cancelled)
        {
            var section = new Section(@event.Id, "Pista", capacity: 5, price: 100m);
            Repository.Sections.Add(section);
            Repository.Tickets.AddRange(section.GenerateTickets());
        }

        switch (status)
        {
            case EventStatus.Published:
                @event.Publish();
                break;
            case EventStatus.Closed:
                @event.Publish();
                @event.Close();
                break;
            case EventStatus.Cancelled:
                @event.Cancel();
                break;
        }

        return @event;
    }
}
