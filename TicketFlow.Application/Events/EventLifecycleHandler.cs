using TicketFlow.Application.Common;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.Application.Events;

// Publicar, encerrar e cancelar compartilham o mesmo roteiro: carregar,
// checar permissão, aplicar a transição e gravar com concorrência otimista.
public class EventLifecycleHandler(IEventRepository events)
{
    public Task<EventSummary> PublishAsync(Actor actor, Guid eventId, CancellationToken cancellationToken) =>
        TransitionAsync(actor, eventId, async (@event, ct) =>
        {
            // Um evento sem setores não tem nada para vender.
            if (await events.CountSectionsAsync(@event.Id, ct) == 0)
                throw new DomainException("EVENT_NEEDS_SECTIONS", "An event needs at least one section to be published.");

            @event.Publish();
        }, cancellationToken);

    public Task<EventSummary> CloseAsync(Actor actor, Guid eventId, CancellationToken cancellationToken) =>
        TransitionAsync(actor, eventId, (@event, _) =>
        {
            @event.Close();
            return Task.CompletedTask;
        }, cancellationToken);

    // Ainda não mexe em tickets, reservas nem pedidos do evento: isso entra
    // junto com as Fases 4 a 6 (reservas, pedidos, pagamentos/reembolso).
    public Task<EventSummary> CancelAsync(Actor actor, Guid eventId, CancellationToken cancellationToken) =>
        TransitionAsync(actor, eventId, (@event, _) =>
        {
            @event.Cancel();
            return Task.CompletedTask;
        }, cancellationToken);

    private async Task<EventSummary> TransitionAsync(
        Actor actor,
        Guid eventId,
        Func<Event, CancellationToken, Task> apply,
        CancellationToken cancellationToken)
    {
        var @event = EventAccess.RequireManage(actor, await events.FindByIdAsync(eventId, cancellationToken));

        await apply(@event, cancellationToken);

        try
        {
            await events.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            // Outra requisição alterou o evento no meio do caminho (ex.: um
            // setor foi adicionado enquanto este pedido publicava).
            throw new EventModifiedException();
        }

        return new EventSummary(@event.Id, @event.OrganizerId, @event.Name, @event.Location, @event.Date, @event.Status);
    }
}
