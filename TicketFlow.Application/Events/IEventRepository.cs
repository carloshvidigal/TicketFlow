using TicketFlow.Application.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Events;

public record EventSummary(Guid Id, Guid OrganizerId, string Name, string Location, DateTime Date, EventStatus Status);

public record SectionAvailability(Guid Id, string Name, int Capacity, decimal Price, int Available);

public record EventDetail(
    Guid Id,
    Guid OrganizerId,
    string Name,
    string Location,
    DateTime Date,
    EventStatus Status,
    IReadOnlyList<SectionAvailability> Sections);

public interface IEventRepository
{
    // Devolve a entidade rastreada: alterar + SaveChangesAsync grava a mudança.
    Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Event @event);

    Task<int> CountSectionsAsync(Guid eventId, CancellationToken cancellationToken);

    // Lança ConcurrentUpdateException se outra requisição alterou o evento
    // entre a leitura e a gravação (concorrência otimista).
    Task SaveChangesAsync(CancellationToken cancellationToken);

    // Grava o setor e seus tickets numa única transação. A implementação
    // precisa verificar, DENTRO dessa transação e com o evento travado, que:
    // o evento ainda é rascunho, o limite de setores não foi atingido e o nome
    // do setor é único no evento — assim duas requisições simultâneas (ou uma
    // publicação concorrente) não furam as regras.
    // Lança DomainException (EVENT_NOT_EDITABLE, EVENT_SECTION_LIMIT_REACHED)
    // ou ConflictException (SECTION_NAME_ALREADY_EXISTS).
    Task AddSectionAsync(Guid eventId, Section section, IReadOnlyList<Ticket> tickets, CancellationToken cancellationToken);

    Task<PagedResult<EventSummary>> ListPublishedAsync(DateTime now, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectionAvailability>> GetSectionsAsync(Guid eventId, CancellationToken cancellationToken);
}
