using TicketFlow.Application.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Events;

// Regras de quem pode ver e gerenciar um evento (§18 do documento): um
// organizador gerencia os eventos que criou; o administrador, todos.
// A Api também restringe por papel, mas esta é a regra de fato — vale mesmo
// que outro ponto de entrada (job, CLI...) chame os casos de uso.
internal static class EventAccess
{
    public static bool CanCreate(Actor actor) => actor.Role is UserRole.Organizer or UserRole.Admin;

    public static bool CanManage(Actor? actor, Event @event) =>
        actor is not null
        && (actor.Role == UserRole.Admin
            || (actor.Role == UserRole.Organizer && @event.OrganizerId == actor.UserId));

    // Publicado ou encerrado: qualquer um vê. Rascunho e cancelado: só quem gerencia.
    public static bool CanView(Actor? actor, Event @event) =>
        @event.Status is EventStatus.Published or EventStatus.Closed || CanManage(actor, @event);

    public static void RequireCreate(Actor actor)
    {
        if (!CanCreate(actor))
            throw new ForbiddenException("FORBIDDEN", "You do not have permission to perform this action.");
    }

    // Quem não pode nem ver o evento recebe 404 (não revela que ele existe);
    // quem pode ver mas não gerenciar recebe 403.
    public static Event RequireManage(Actor actor, Event? @event)
    {
        if (@event is null || !CanView(actor, @event))
            throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");

        if (!CanManage(actor, @event))
            throw new ForbiddenException("FORBIDDEN", "You do not have permission to perform this action.");

        return @event;
    }
}
