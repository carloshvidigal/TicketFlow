using TicketFlow.Application.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.Application.Events;

public record AddSectionCommand(Actor Actor, Guid EventId, string Name, int Capacity, decimal Price);

public class AddSectionHandler(IEventRepository events)
{
    public async Task<SectionAvailability> HandleAsync(AddSectionCommand command, CancellationToken cancellationToken)
    {
        var @event = EventAccess.RequireManage(
            command.Actor, await events.FindByIdAsync(command.EventId, cancellationToken));

        // Falha cedo e com a mensagem certa; a repetição desta checagem (agora
        // sem brecha para corrida) acontece dentro da transação do repositório.
        @event.EnsureEditable();

        var section = new Section(@event.Id, command.Name, command.Capacity, command.Price);
        var tickets = section.GenerateTickets();

        await events.AddSectionAsync(@event.Id, section, tickets, cancellationToken);

        // Setor recém-criado: todos os ingressos estão disponíveis.
        return new SectionAvailability(section.Id, section.Name, section.Capacity, section.Price, section.Capacity);
    }
}
