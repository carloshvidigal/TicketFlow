using TicketFlow.Application.Common;

namespace TicketFlow.Application.Events;

public class EventQueries(IEventRepository events, TimeProvider clock)
{
    // Só eventos publicados e que ainda não aconteceram, do mais próximo ao mais distante.
    public Task<PagedResult<EventSummary>> ListPublishedAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        events.ListPublishedAsync(clock.GetUtcNow().UtcDateTime, page, pageSize, cancellationToken);

    // actor é nulo para quem não está autenticado.
    public async Task<EventDetail> GetAsync(Actor? actor, Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await events.FindByIdAsync(eventId, cancellationToken);

        if (@event is null || !EventAccess.CanView(actor, @event))
            throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");

        var sections = await events.GetSectionsAsync(eventId, cancellationToken);

        return new EventDetail(
            @event.Id, @event.OrganizerId, @event.Name, @event.Location, @event.Date, @event.Status, sections);
    }
}
