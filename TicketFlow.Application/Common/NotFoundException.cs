namespace TicketFlow.Application.Common;

// O recurso não existe — ou o ator não tem permissão nem para saber que existe
// (ex.: rascunho de outro organizador). A Api traduz para HTTP 404.
public class NotFoundException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
