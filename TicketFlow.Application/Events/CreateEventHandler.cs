using TicketFlow.Application.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.Application.Events;

public record CreateEventCommand(Actor Actor, string Name, string Location, DateTime Date);

public class CreateEventHandler(IEventRepository events)
{
    public async Task<EventSummary> HandleAsync(CreateEventCommand command, CancellationToken cancellationToken)
    {
        EventAccess.RequireCreate(command.Actor);

        // O organizador do evento é sempre quem está autenticado, nunca um
        // valor vindo do corpo da requisição.
        var @event = new Event(command.Actor.UserId, command.Name, command.Location, command.Date);

        events.Add(@event);
        await events.SaveChangesAsync(cancellationToken);

        return new EventSummary(@event.Id, @event.OrganizerId, @event.Name, @event.Location, @event.Date, @event.Status);
    }
}
