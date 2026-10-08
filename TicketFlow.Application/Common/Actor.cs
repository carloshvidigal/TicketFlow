using TicketFlow.Domain.Users;

namespace TicketFlow.Application.Common;

// Quem está executando a operação. Montado pela Api a partir do JWT já
// validado; a Application decide o que esse ator pode fazer com cada recurso.
public record Actor(Guid UserId, UserRole Role);
