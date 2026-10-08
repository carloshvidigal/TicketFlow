using TicketFlow.Application.Common;

namespace TicketFlow.Application.Events;

public record UpdateEventCommand(Actor Actor, Guid EventId, string Name, string Location, DateTime Date);

public class UpdateEventHandler(IEventRepository events)
{
    public async Task<EventSummary> HandleAsync(UpdateEventCommand command, CancellationToken cancellationToken)
    {
        var @event = EventAccess.RequireManage(
            command.Actor, await events.FindByIdAsync(command.EventId, cancellationToken));

        @event.Update(command.Name, command.Location, command.Date);

        try
        {
            await events.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            throw new EventModifiedException();
        }

        return new EventSummary(@event.Id, @event.OrganizerId, @event.Name, @event.Location, @event.Date, @event.Status);
    }
}
