namespace TicketFlow.Application.Common;

// O ator está autenticado e pode ver o recurso, mas não pode fazer esta
// operação nele. A Api traduz para HTTP 403.
public class ForbiddenException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
